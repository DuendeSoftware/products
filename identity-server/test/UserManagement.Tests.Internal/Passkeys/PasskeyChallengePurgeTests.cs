// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Hosting;
using Duende.IdentityServer.Infrastructure.DistributedLock;
using Duende.Storage;
using Duende.Storage.Internal;
using Duende.Storage.Internal.Operations;
using Duende.UserManagement;
using Duende.UserManagement.Authentication.Passkeys.Internal;
using Duende.UserManagement.Authentication.Passkeys.Internal.Storage;
using Duende.UserManagement.Internal.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Duende.Platform.UserManagement.Passkeys;

/// <summary>
/// Integration tests proving that StoragePurgeHost physically removes expired passkey
/// registration and authentication challenges, which set a relative TTL
/// (<c>Expiration.InRelative(options.Passkeys.ChallengeTimeout)</c>) at the Duende Storage level.
/// </summary>
/// <remarks>
/// These stores don't filter expired reads themselves (unlike e.g. the SAML stores), but the
/// physical row is still read directly through the underlying partitioned storage, the same way
/// the operational-data purge tests do, so the assertion proves actual deletion rather than a
/// read-time filter.
/// </remarks>
public sealed class PasskeyChallengePurgeTests : IAsyncLifetime
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;
    private ServiceProvider _serviceProvider = null!;
    private FakeTimeProvider _clock = null!;

    public async ValueTask InitializeAsync()
    {
        _serviceProvider = await UsersServiceProviderFactory.CreateAsync();
        _clock = _serviceProvider.GetRequiredService<FakeTimeProvider>();
    }

    public ValueTask DisposeAsync() => _serviceProvider.DisposeAsync();

    [Fact]
    public async Task run_purge_physically_removes_expired_registration_challenge()
    {
        var store = _serviceProvider.GetRequiredService<IPasskeyRegistrationChallengeStore>();
        var userSubjectId = UserSubjectId.New();

        var expiredChallenge = PasskeyRegistrationChallenge.Create("expired-challenge", userSubjectId, _clock.GetUtcNow());
        (await store.CreateAsync(expiredChallenge, _ct)).ShouldBe(CreateResult.Success);

        (await TryReadAsync(PasskeyRegistrationChallengeDso.EntityType, expiredChallenge.Id.Uuid.Value)).Found.ShouldBeTrue();

        // Default ChallengeTimeout is 300 seconds (PasskeyOptions.ChallengeTimeout). Advance past
        // it, then store a second challenge from the new clock reading as a control: it must
        // survive the same purge run that removes the first.
        _clock.Advance(TimeSpan.FromSeconds(301));

        var validChallenge = PasskeyRegistrationChallenge.Create("valid-challenge", userSubjectId, _clock.GetUtcNow());
        (await store.CreateAsync(validChallenge, _ct)).ShouldBe(CreateResult.Success);

        await CreatePurgeHost().RunPurgeAsync(_ct);

        (await TryReadAsync(PasskeyRegistrationChallengeDso.EntityType, expiredChallenge.Id.Uuid.Value)).Found.ShouldBeFalse(
            "expired registration challenge must be physically purged");
        (await TryReadAsync(PasskeyRegistrationChallengeDso.EntityType, validChallenge.Id.Uuid.Value)).Found.ShouldBeTrue(
            "a challenge created after the clock advance must survive purge");
    }

    [Fact]
    public async Task run_purge_physically_removes_expired_authentication_challenge()
    {
        var store = _serviceProvider.GetRequiredService<IPasskeyAuthenticationChallengeStore>();

        var expiredChallenge = PasskeyAuthenticationChallenge.CreateDiscoverable("expired-challenge", _clock.GetUtcNow());
        (await store.CreateAsync(expiredChallenge, _ct)).ShouldBe(CreateResult.Success);

        _clock.Advance(TimeSpan.FromSeconds(301));

        var validChallenge = PasskeyAuthenticationChallenge.CreateDiscoverable("valid-challenge", _clock.GetUtcNow());
        (await store.CreateAsync(validChallenge, _ct)).ShouldBe(CreateResult.Success);

        (await TryReadAsync(PasskeyAuthenticationChallengeDso.EntityType, expiredChallenge.Id.Uuid.Value)).Found.ShouldBeTrue();
        (await TryReadAsync(PasskeyAuthenticationChallengeDso.EntityType, validChallenge.Id.Uuid.Value)).Found.ShouldBeTrue();

        await CreatePurgeHost().RunPurgeAsync(_ct);

        (await TryReadAsync(PasskeyAuthenticationChallengeDso.EntityType, expiredChallenge.Id.Uuid.Value)).Found.ShouldBeFalse(
            "expired authentication challenge must be physically purged");
        (await TryReadAsync(PasskeyAuthenticationChallengeDso.EntityType, validChallenge.Id.Uuid.Value)).Found.ShouldBeTrue(
            "unexpired authentication challenge must survive purge");
    }

    private async Task<StoreGetResult> TryReadAsync(EntityType entityType, Guid id)
    {
        var factory = _serviceProvider.GetRequiredService<IPartitionedStorageFactory>();
        var storage = await factory.GetPartitionedStorageAsync(DataCategoryName.UserManagement, _ct);
        return await storage.TryReadAsync(entityType, UuidV7.From(id), _ct);
    }

    private StoragePurgeHost CreatePurgeHost() => new(
        _serviceProvider.GetRequiredService<IStorageInstanceRouter>(),
        _serviceProvider.GetRequiredService<ICrossPartitionStorageFactory>(),
        new IdentityServerOptions(),
        new InProcessDistributedLock(),
        _clock,
        _serviceProvider.GetRequiredService<ILogger<StoragePurgeHost>>());
}
