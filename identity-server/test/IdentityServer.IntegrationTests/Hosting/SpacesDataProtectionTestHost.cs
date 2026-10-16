// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.IntegrationTests.TestFramework;
using Duende.IdentityServer.IntegrationTests.TestFramework.TestIsolation;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services.KeyManagement;
using Duende.IdentityServer.Stores.Storage.SigningKeys;
using Duende.Spaces;
using Duende.Storage.Schema;
using Duende.Storage.Sqlite;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using StorageOutboxProcessor = Duende.Storage.Internal.Outbox.OutboxProcessor;

namespace Duende.IdentityServer.IntegrationTests.Hosting;

/// <summary>A space of a <see cref="SpacesDataProtectionTestHost"/> and how it is reached.</summary>
internal sealed record SpacesTestSpace(SpaceId Id, string Prefix, string Issuer);

/// <summary>
/// A small IdentityServer host with Spaces (resolved by path) for the data protection tests. It has a local
/// sign-in endpoint that issues the IdentityServer session cookie and a code flow client, so a test can sign in
/// in one space and check whether the cookie is authenticated in another.
/// </summary>
internal sealed class SpacesDataProtectionTestHost : IAsyncDisposable
{
    public const string ClientId = "client";
    public const string Subject = "alice";
    public const string RedirectUri = "https://rp.example/callback";

    private const string CodeChallenge = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQ";

    private readonly KestrelBasedTestServer _server;
    private readonly SpacesTestSharedStorage? _ownedStorage;
    private readonly SpacesTestSpace? _spaceA;
    private readonly SpacesTestSpace? _spaceB;
    private readonly List<HttpClient> _clients = [];

    private SpacesDataProtectionTestHost(
        KestrelBasedTestServer server, SpacesTestSharedStorage? ownedStorage, SpacesTestSpace? spaceA, SpacesTestSpace? spaceB)
    {
        _server = server;
        _ownedStorage = ownedStorage;
        _spaceA = spaceA;
        _spaceB = spaceB;
    }

    public IServiceProvider Services => _server.Services;

    public SpacesTestSpace DefaultSpace { get; private init; } = null!;

    /// <summary>Throws on a host without Spaces.</summary>
    public SpacesTestSpace SpaceA => _spaceA ?? throw HostHasNoSpaces();

    /// <summary>Throws on a host without Spaces.</summary>
    public SpacesTestSpace SpaceB => _spaceB ?? throw HostHasNoSpaces();

    public static Task<SpacesDataProtectionTestHost> CreateAsync(WebServerFixture webServerFixture, string name, Ct ct) =>
        CreateAsync(webServerFixture, name, new SpacesDataProtectionTestHostOptions(), ct);

    public static async Task<SpacesDataProtectionTestHost> CreateAsync(
        WebServerFixture webServerFixture, string name, SpacesDataProtectionTestHostOptions options, Ct ct)
    {
        var ownedStorage = options.SharedStorage == null ? new SpacesTestSharedStorage() : null;
        var storage = options.SharedStorage ?? ownedStorage!;
        var output = TestContext.Current.TestOutputHelper!;
        var clock = new FakeTimeProvider(DateTime.UtcNow);

        var server = new KestrelBasedTestServer(
            $"ms{Guid.NewGuid():N}"[..10],
            webServerFixture,
            new PrefixedTestOutputHelper(output, name),
            services =>
            {
                // Registered before AddSpaces(): its startup check fails when a non-keyed
                // IDataProtectionProvider is added after it.
                if (options.KeyRing is { } keyRing)
                {
                    _ = services.AddDataProtection()
                        .PersistKeysToFileSystem(new DirectoryInfo(keyRing.DirectoryPath))
                        .SetApplicationName(keyRing.ApplicationName);
                }

                services.AddRouting();
                services.AddSingleton<TimeProvider>(clock);

                var builder = services.AddIdentityServer(identityServerOptions => Configure(identityServerOptions, options, storage))
                    .AddInMemoryClients([
                        new Client
                        {
                            ClientId = ClientId,
                            AllowedGrantTypes = GrantTypes.Code,
                            RequireClientSecret = false,
                            RequirePkce = true,
                            RequireConsent = false,
                            RedirectUris = { RedirectUri },
                            AllowedScopes = { "openid" }
                        }
                    ])
                    .AddInMemoryIdentityResources([new IdentityResources.OpenId()]);

                if (options.SigningKeyStore == SpacesSigningKeyStore.DeveloperCredential)
                {
                    _ = builder.AddDeveloperSigningCredential(persistKey: false);
                }

                _ = builder.AddStorage(storageBuilder => storageBuilder.AddSqliteInMemory(storage.DatabaseName))
                    .AddConfigurationStorage()
                    .AddOperationalStorage();

                // Registered after AddOperationalStorage() so the explicit choice wins over its default.
                if (options.SigningKeyStore == SpacesSigningKeyStore.FileSystem)
                {
                    _ = builder.AddSigningKeyStore<FileSystemKeyStore>();
                }
                else if (options.SigningKeyStore == SpacesSigningKeyStore.Storage)
                {
                    _ = builder.AddSigningKeyStore<SigningKeyStore>();
                }

                if (options.ServerSideSessions)
                {
                    _ = builder.AddServerSideSessions();
                }

                if (options.BackChannelLogoutService is { } backChannelLogoutService)
                {
                    services.AddSingleton(backChannelLogoutService);
                }

                if (options.EnableSpaces)
                {
                    _ = builder.AddSpaces(spaces =>
                    {
                        spaces.PartitionDataProtection = options.PartitionDataProtection;
                        spaces.FallbackToDefault = options.FallbackToDefault;
                    });
                }
            },
            webapp =>
            {
                if (options.EnableSpaces)
                {
                    webapp.UseSpaceResolution();
                }

                webapp.UseIdentityServer();

                // Plain middleware rather than endpoint routing, so it sees the path after space resolution.
                webapp.Use(async (ctx, next) =>
                {
                    if (ctx.Request.Path.Value?.EndsWith("/test/signin", StringComparison.Ordinal) == true)
                    {
                        var subject = ctx.Request.Query["sub"].FirstOrDefault() ?? Subject;
                        await ctx.SignInAsync(new IdentityServerUser(subject));
                        ctx.Response.StatusCode = StatusCodes.Status200OK;
                        return;
                    }

                    await next(ctx);
                });
            });

        try
        {
            await server.StartAsync();
            await server.GetRequiredService<IStorageInstanceSchema>().MigrateAsync(ct);

            var defaultSpace = CreateSpace(server, SpaceId.Default, "");
            if (!options.EnableSpaces)
            {
                return new SpacesDataProtectionTestHost(server, ownedStorage, null, null) { DefaultSpace = defaultSpace };
            }

            // A host that shares storage with an earlier host reuses the spaces that host created.
            if (storage.SpaceAId == null || storage.SpaceBId == null)
            {
                var spaceAdmin = server.GetRequiredService<ISpaceAdmin>();
                storage.SpaceAId = await CreateSpaceAsync(spaceAdmin, "Space A", "/space-a", ct);
                storage.SpaceBId = await CreateSpaceAsync(spaceAdmin, "Space B", "/space-b", ct);
            }

            return new SpacesDataProtectionTestHost(
                server,
                ownedStorage,
                CreateSpace(server, storage.SpaceAId.Value, "/t/space-a"),
                CreateSpace(server, storage.SpaceBId.Value, "/t/space-b"))
            {
                DefaultSpace = defaultSpace
            };
        }
        catch
        {
            await server.DisposeAsync();
            ownedStorage?.Dispose();
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var client in _clients)
        {
            client.Dispose();
        }

        await _server.DisposeAsync();
        _ownedStorage?.Dispose();
    }

    /// <summary>Signs in at the space and returns the session cookies the server issued.</summary>
    public async Task<Dictionary<string, string>> SignInAsync(SpacesTestSpace space, Ct ct)
    {
        using var response = await SendAsync($"{space.Prefix}/test/signin", new Dictionary<string, string>(), ct);
        response.EnsureSuccessStatusCode();

        var cookies = new Dictionary<string, string>();
        foreach (var header in response.Headers.GetValues("Set-Cookie"))
        {
            var pair = header.Split(';')[0];
            var separator = pair.IndexOf('=', StringComparison.Ordinal);
            cookies[pair[..separator]] = pair[(separator + 1)..];
        }

        cookies.ShouldContainKey("idsrv", "the sign-in must have issued the authentication cookie");
        return cookies;
    }

    /// <summary>
    /// Calls the authorize endpoint of the space with the cookies in a raw Cookie header. An authenticated
    /// cookie is answered with a redirect carrying a code; anything else redirects to the login page.
    /// </summary>
    public Task<HttpResponseMessage> AuthorizeAsync(SpacesTestSpace space, IReadOnlyDictionary<string, string> cookies, Ct ct) =>
        SendAsync(
            $"{space.Prefix}/connect/authorize?client_id={ClientId}&response_type=code&scope=openid" +
            $"&redirect_uri={Uri.EscapeDataString(RedirectUri)}&code_challenge={CodeChallenge}&code_challenge_method=S256&state=s1",
            cookies,
            ct);

    /// <summary>Sends a GET without cookies to the space.</summary>
    public async Task<string> GetStringAsync(SpacesTestSpace space, string path, Ct ct)
    {
        using var response = await SendAsync($"{space.Prefix}{path}", new Dictionary<string, string>(), ct);
        return await response.Content.ReadAsStringAsync(ct);
    }

    /// <summary>
    /// Runs the action in a fresh scope of the root service provider with the space set on
    /// <see cref="ISpaceContextAccessor"/>, as space resolution does for a request.
    /// </summary>
    public async Task<T> RunInSpaceAsync<T>(SpaceId spaceId, Func<IServiceProvider, Task<T>> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        await using var scope = Services.CreateAsyncScope();
        var accessor = scope.ServiceProvider.GetRequiredService<ISpaceContextAccessor>();
        using (accessor.SetSpace(spaceId))
        {
            return await action(scope.ServiceProvider);
        }
    }

    public async Task RunInSpaceAsync(SpaceId spaceId, Func<IServiceProvider, Task> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        _ = await RunInSpaceAsync(spaceId, async services =>
        {
            await action(services);
            return true;
        });
    }

    /// <summary>
    /// Runs one pass of the outbox processor, the same call the background host makes on its timer. With
    /// server-side sessions the background processor is off, so this is the only way events are processed.
    /// </summary>
    public Task RunOutboxProcessorAsync(Ct ct) =>
        Services.GetRequiredService<StorageOutboxProcessor>().RunProcessorAsync(ct);

    private static void Configure(IdentityServerOptions identityServerOptions, SpacesDataProtectionTestHostOptions options, SpacesTestSharedStorage storage)
    {
        identityServerOptions.KeyManagement.Enabled = options.SigningKeyStore != SpacesSigningKeyStore.DeveloperCredential;
        identityServerOptions.KeyManagement.DataProtectKeys = true;
        if (options.SigningKeyStore == SpacesSigningKeyStore.FileSystem)
        {
            identityServerOptions.KeyManagement.KeyPath = storage.SigningKeyDirectory;
        }

        if (options.ServerSideSessions)
        {
            identityServerOptions.OutboxProcessor.EnableProcessor = false;
        }

        if (options.BackChannelLogoutService != null)
        {
            identityServerOptions.ServerSideSessions.ExpiredSessionsTriggerBackchannelLogout = true;
        }
    }

    private async Task<HttpResponseMessage> SendAsync(string url, IReadOnlyDictionary<string, string> cookies, Ct ct)
    {
        var client = new HttpClient(_server.CreateHandler(allowAutoRedirect: false), disposeHandler: true) { BaseAddress = _server.BaseAddress };
        _clients.Add(client);

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (cookies.Count > 0)
        {
            request.Headers.TryAddWithoutValidation("Cookie", string.Join("; ", cookies.Select(c => $"{c.Key}={c.Value}")));
        }

        return await client.SendAsync(request, ct);
    }

    private static SpacesTestSpace CreateSpace(KestrelBasedTestServer server, SpaceId id, string prefix) =>
        new(id, prefix, server.BuildUrl(prefix).ToString().TrimEnd('/'));

    private static async Task<SpaceId> CreateSpaceAsync(ISpaceAdmin spaceAdmin, string name, string path, Ct ct)
    {
        var result = await spaceAdmin.CreateAsync(
            new CreateSpaceConfiguration { Name = name, MatchPatterns = [new SpaceMatchPattern { Path = path }] },
            ct);
        result.IsSuccess.ShouldBeTrue($"Create {name} failed: {result}");
        return result.Id!.Value;
    }

    private static InvalidOperationException HostHasNoSpaces() =>
        new("This host was created with EnableSpaces = false, so it has no spaces.");
}
