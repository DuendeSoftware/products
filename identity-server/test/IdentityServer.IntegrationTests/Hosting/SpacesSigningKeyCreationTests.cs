// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.IntegrationTests.TestFramework;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services.KeyManagement;
using Duende.IdentityServer.Stores;
using Duende.IdentityServer.Stores.Storage.SigningKeys;
using Duende.Spaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Duende.IdentityServer.IntegrationTests.Hosting;

public sealed class SpacesSigningKeyCreationTests(WebServerFixture webServerFixture)
{
    private static readonly TimeSpan FailureBound = TimeSpan.FromSeconds(30);

    private const string JwksPath = "/.well-known/openid-configuration/jwks";

    private readonly Ct _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task a_space_creating_keys_does_not_block_other_spaces_or_the_default_space()
    {
        var pause = new PausedSpace();
        await using var host = await CreateHostAsync(pause);
        pause.Space = host.SpaceA.Id;

        try
        {
            var requestA = host.GetStringAsync(host.SpaceA, JwksPath, _ct);
            await pause.Entered.Task.WaitAsync(FailureBound, _ct);

            var jwksB = await host.GetStringAsync(host.SpaceB, JwksPath, _ct).WaitAsync(FailureBound, _ct);
            var jwksDefault = await host.GetStringAsync(host.DefaultSpace, JwksPath, _ct).WaitAsync(FailureBound, _ct);

            var storedB = await LoadStoredKeyIdsAsync(host, host.SpaceB);
            storedB.Length.ShouldBe(1);
            KeyIds(jwksB).ShouldBe(storedB);
            KeyIds(jwksDefault).ShouldBe(await LoadStoredKeyIdsAsync(host, host.DefaultSpace));

            pause.Release.TrySetResult();
            var jwksA = await requestA.WaitAsync(FailureBound, _ct);

            var storedA = await LoadStoredKeyIdsAsync(host, host.SpaceA);
            storedA.Length.ShouldBe(1);
            KeyIds(jwksA).ShouldBe(storedA);
            storedA.ShouldNotBe(storedB, "each Space must have its own key");
        }
        finally
        {
            pause.Release.TrySetResult();
        }
    }

    [Fact]
    public async Task concurrent_requests_in_one_space_create_one_key()
    {
        var pause = new PausedSpace();
        await using var host = await CreateHostAsync(pause);
        pause.Space = host.SpaceA.Id;

        try
        {
            var request1 = host.GetStringAsync(host.SpaceA, JwksPath, _ct);
            await pause.Entered.Task.WaitAsync(FailureBound, _ct);

            var request2 = host.GetStringAsync(host.SpaceA, JwksPath, _ct);
            await pause.EmptyReadSeen.Task.WaitAsync(FailureBound, _ct);

            pause.Release.TrySetResult();
            var jwks1 = await request1.WaitAsync(FailureBound, _ct);
            var jwks2 = await request2.WaitAsync(FailureBound, _ct);

            var stored = await LoadStoredKeyIdsAsync(host, host.SpaceA);
            stored.Length.ShouldBe(1);
            KeyIds(jwks1).ShouldBe(stored);
            KeyIds(jwks2).ShouldBe(stored);
        }
        finally
        {
            pause.Release.TrySetResult();
        }
    }

    [Fact]
    public async Task a_request_without_a_space_shares_the_default_space_key()
    {
        var pause = new PausedSpace { Space = SpaceId.Default };
        await using var host = await CreateHostAsync(pause);

        try
        {
            var defaultSpaceRequest = host.GetStringAsync(host.DefaultSpace, JwksPath, _ct);
            await pause.Entered.Task.WaitAsync(FailureBound, _ct);

            var noSpaceRequest = GetCurrentKeysWithoutSpaceAsync(host);
            await pause.EmptyReadSeen.Task.WaitAsync(FailureBound, _ct);

            pause.Release.TrySetResult();
            var jwks = await defaultSpaceRequest.WaitAsync(FailureBound, _ct);
            var keysWithoutSpace = await noSpaceRequest.WaitAsync(FailureBound, _ct);

            var stored = await LoadStoredKeyIdsAsync(host, host.DefaultSpace);
            stored.Length.ShouldBe(1);
            KeyIds(jwks).ShouldBe(stored);
            keysWithoutSpace.Select(k => k.Id).Order().ToArray().ShouldBe(stored);
        }
        finally
        {
            pause.Release.TrySetResult();
        }
    }

    private Task<SpacesDataProtectionTestHost> CreateHostAsync(PausedSpace pause) =>
        SpacesDataProtectionTestHost.CreateAsync(webServerFixture, nameof(SpacesSigningKeyCreationTests), new SpacesDataProtectionTestHostOptions
        {
            SigningKeyStore = SpacesSigningKeyStore.Storage,
            FallbackToDefault = true,
            ConfigureKeyManagement = key => key.InitializationSynchronizationDelay = TimeSpan.Zero,
            ConfigureServices = services => DecorateSigningKeyStore(services, pause)
        }, _ct);

    private static void DecorateSigningKeyStore(IServiceCollection services, PausedSpace pause)
    {
        var registration = services.Last(d => d.ServiceType == typeof(ISigningKeyStore));
        registration.ImplementationType.ShouldBe(typeof(SigningKeyStore));
        _ = services.Remove(registration);
        services.Add(new ServiceDescriptor(
            typeof(ISigningKeyStore),
            sp => new PausingSigningKeyStore(
                ActivatorUtilities.CreateInstance<SigningKeyStore>(sp),
                pause,
                sp.GetRequiredService<ISpaceContextAccessor>()),
            registration.Lifetime));
    }

    private static async Task<IReadOnlyList<KeyContainer>> GetCurrentKeysWithoutSpaceAsync(SpacesDataProtectionTestHost host)
    {
        await using var scope = host.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ISpaceContextAccessor>().IsSpaceIdConfigured()
            .ShouldBeFalse("this scope must have no Space");
        var keyManager = scope.ServiceProvider.GetRequiredService<IKeyManager>();
        return [.. await keyManager.GetCurrentKeysAsync(TestContext.Current.CancellationToken)];
    }

    private static string[] KeyIds(string jwks) => [.. new JsonWebKeySet(jwks).Keys.Select(k => k.Kid).Order()];

    private static async Task<string[]> LoadStoredKeyIdsAsync(SpacesDataProtectionTestHost host, SpacesTestSpace space) =>
        [.. (await host.RunInSpaceAsync<IReadOnlyCollection<SerializedKey>>(space.Id, services =>
            services.GetRequiredService<ISigningKeyStore>().LoadKeysAsync(TestContext.Current.CancellationToken)))
            .Select(k => k.Id).Order()];

    private sealed class PausedSpace
    {
        public SpaceId? Space { get; set; }

        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource EmptyReadSeen { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class PausingSigningKeyStore(ISigningKeyStore inner, PausedSpace pause, ISpaceContextAccessor accessor) : ISigningKeyStore
    {
        public async Task<IReadOnlyCollection<SerializedKey>> LoadKeysAsync(Ct ct)
        {
            var keys = await inner.LoadKeysAsync(ct);
            if (keys.Count == 0 && pause.Entered.Task.IsCompleted && accessor.GetSpaceIdOrDefault() == pause.Space)
            {
                _ = pause.EmptyReadSeen.TrySetResult();
            }

            return keys;
        }

        public async Task StoreKeyAsync(SerializedKey key, Ct ct)
        {
            if (accessor.GetSpaceIdOrDefault() == pause.Space)
            {
                _ = pause.Entered.TrySetResult();
                await pause.Release.Task.WaitAsync(ct);
            }

            await inner.StoreKeyAsync(key, ct);
        }

        public Task DeleteKeyAsync(string id, Ct ct) => inner.DeleteKeyAsync(id, ct);
    }
}
