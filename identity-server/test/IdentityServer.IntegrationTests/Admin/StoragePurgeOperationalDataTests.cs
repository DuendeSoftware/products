// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Hosting;
using Duende.IdentityServer.Infrastructure.DistributedLock;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Saml;
using Duende.IdentityServer.Stores.Storage;
using Duende.IdentityServer.Stores.Storage.DeviceFlow;
using Duende.IdentityServer.Stores.Storage.PushedAuthorization;
using Duende.IdentityServer.Stores.Storage.SamlLogoutSession;
using Duende.Storage;

namespace Duende.IdentityServer.IntegrationTests.Admin;

/// <summary>
/// Integration tests proving that StoragePurgeHost physically removes expired entities for
/// operational data types that set a TTL at the Duende Storage level but are not covered by
/// <see cref="StoragePurgeHostTests"/> or <c>SeparateDatabaseStorageTests</c>: device codes,
/// pushed authorization requests, SAML signin state, and SAML logout sessions.
/// </summary>
/// <remarks>
/// Several of these stores hide expired-but-not-yet-purged entries from their own read APIs
/// (SAML signin state and logout sessions check <c>ExpiresAtUtc</c> against the current time on
/// every read). That makes a read-returns-null assertion alone insufficient to prove purging
/// actually ran. Each test here instead reads the underlying partitioned storage directly, by
/// entity type and storage key, to prove the row itself is gone.
/// </remarks>
public sealed class StoragePurgeOperationalDataTests : IAsyncLifetime
{
    private readonly StorageTestFixture _fixture = new();
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync() => _fixture.InitializeAsync();
    public ValueTask DisposeAsync() => _fixture.DisposeAsync();

    [Fact]
    public async Task run_purge_physically_removes_expired_device_code()
    {
        var store = _fixture.DeviceFlowStore;
        var expiredDeviceCode = $"device_{Guid.NewGuid():N}";
        var expiredUserCode = $"user_{Guid.NewGuid():N}";
        var validDeviceCode = $"device_{Guid.NewGuid():N}";
        var validUserCode = $"user_{Guid.NewGuid():N}";

        // DeviceFlowStore computes expiration from CreationTime + Lifetime. The store treats an
        // already-expired create as a no-op (mirrors persisted grants), so store with a lifetime
        // that expires shortly in the future and advance the clock past it below.
        await store.StoreDeviceAuthorizationAsync(expiredDeviceCode, expiredUserCode, new DeviceCode
        {
            ClientId = "client",
            CreationTime = _fixture.Clock.GetUtcNow().UtcDateTime,
            Lifetime = 60, // expires one minute from now
            RequestedScopes = ["openid"]
        }, _ct);

        await store.StoreDeviceAuthorizationAsync(validDeviceCode, validUserCode, new DeviceCode
        {
            ClientId = "client",
            CreationTime = _fixture.Clock.GetUtcNow().UtcDateTime,
            Lifetime = 600,
            RequestedScopes = ["openid"]
        }, _ct);

        // Confirm both rows physically exist before purge.
        (await TryReadDeviceCodeAsync(expiredDeviceCode)).Found.ShouldBeTrue();
        (await TryReadDeviceCodeAsync(validDeviceCode)).Found.ShouldBeTrue();

        _fixture.Clock.Advance(TimeSpan.FromMinutes(2));
        await CreatePurgeHost().RunPurgeAsync(_ct);

        (await TryReadDeviceCodeAsync(expiredDeviceCode)).Found.ShouldBeFalse("expired device code must be physically purged");
        (await TryReadDeviceCodeAsync(validDeviceCode)).Found.ShouldBeTrue("unexpired device code must survive purge");
    }

    [Fact]
    public async Task run_purge_physically_removes_expired_pushed_authorization_request()
    {
        var store = _fixture.PushedAuthorizationRequestStore;

        var expiredHash = $"expired_{Guid.NewGuid():N}";
        var validHash = $"valid_{Guid.NewGuid():N}";

        // The store treats an already-expired ExpiresAtUtc as a no-op create, so store with a
        // future expiration and advance the clock below, mirroring SeparateDatabaseStorageTests.
        await store.StoreAsync(new PushedAuthorizationRequest
        {
            ReferenceValueHash = expiredHash,
            ExpiresAtUtc = _fixture.Clock.GetUtcNow().UtcDateTime.AddMinutes(5),
            Parameters = "protected-params"
        }, _ct);

        await store.StoreAsync(new PushedAuthorizationRequest
        {
            ReferenceValueHash = validHash,
            ExpiresAtUtc = _fixture.Clock.GetUtcNow().UtcDateTime.AddHours(1),
            Parameters = "protected-params"
        }, _ct);

        (await TryReadPushedAuthorizationAsync(expiredHash)).Found.ShouldBeTrue();
        (await TryReadPushedAuthorizationAsync(validHash)).Found.ShouldBeTrue();

        _fixture.Clock.Advance(TimeSpan.FromMinutes(10));
        await CreatePurgeHost().RunPurgeAsync(_ct);

        (await TryReadPushedAuthorizationAsync(expiredHash)).Found.ShouldBeFalse("expired PAR must be physically purged");
        (await TryReadPushedAuthorizationAsync(validHash)).Found.ShouldBeTrue("unexpired PAR must survive purge");
    }

    [Fact]
    public async Task run_purge_physically_removes_expired_saml_signin_state()
    {
        var store = _fixture.SamlSigninStateStore;

        var expiredStateId = await store.StoreSigninRequestStateAsync(BuildSigninState(
            _fixture.Clock.GetUtcNow().UtcDateTime.AddMinutes(5)), _ct);
        var validStateId = await store.StoreSigninRequestStateAsync(BuildSigninState(
            _fixture.Clock.GetUtcNow().UtcDateTime.AddHours(1)), _ct);

        // Reading through the store already hides not-yet-purged expired state, so prove
        // physical presence/absence via the raw partitioned storage instead.
        (await TryReadByIdAsync(SamlSigninStateDso.EntityType, expiredStateId)).Found.ShouldBeTrue();
        (await TryReadByIdAsync(SamlSigninStateDso.EntityType, validStateId)).Found.ShouldBeTrue();

        _fixture.Clock.Advance(TimeSpan.FromMinutes(10));
        await CreatePurgeHost().RunPurgeAsync(_ct);

        (await TryReadByIdAsync(SamlSigninStateDso.EntityType, expiredStateId)).Found.ShouldBeFalse("expired SAML signin state must be physically purged");
        (await TryReadByIdAsync(SamlSigninStateDso.EntityType, validStateId)).Found.ShouldBeTrue("unexpired SAML signin state must survive purge");
    }

    [Fact]
    public async Task run_purge_physically_removes_expired_saml_logout_session()
    {
        var store = _fixture.SamlLogoutSessionStore;

        var expiredLogoutId = $"logout_{Guid.NewGuid():N}";
        var validLogoutId = $"logout_{Guid.NewGuid():N}";

        await store.StoreAsync(BuildLogoutSession(expiredLogoutId, _fixture.Clock.GetUtcNow().UtcDateTime.AddMinutes(5)), _ct);
        await store.StoreAsync(BuildLogoutSession(validLogoutId, _fixture.Clock.GetUtcNow().UtcDateTime.AddHours(1)), _ct);

        // GetByLogoutIdAsync hides not-yet-purged expired sessions, so prove physical
        // presence/absence via the raw partitioned storage instead.
        (await TryReadLogoutSessionAsync(expiredLogoutId)).Found.ShouldBeTrue();
        (await TryReadLogoutSessionAsync(validLogoutId)).Found.ShouldBeTrue();

        _fixture.Clock.Advance(TimeSpan.FromMinutes(10));
        await CreatePurgeHost().RunPurgeAsync(_ct);

        (await TryReadLogoutSessionAsync(expiredLogoutId)).Found.ShouldBeFalse("expired SAML logout session must be physically purged");
        (await TryReadLogoutSessionAsync(validLogoutId)).Found.ShouldBeTrue("unexpired SAML logout session must survive purge");
    }

    private static SamlAuthenticationState BuildSigninState(DateTime expiresAtUtc) => new()
    {
        ServiceProviderEntityId = $"https://sp-{Guid.NewGuid():N}.example.com",
        AssertionConsumerService = new IndexedEndpoint
        {
            Location = "https://sp.example.com/saml/acs",
            Binding = SamlBinding.HttpPost,
            Index = 0,
            IsDefault = true
        },
        CreatedUtc = DateTimeOffset.UtcNow,
        ExpiresAtUtc = expiresAtUtc,
        IsIdpInitiated = false
    };

    private static Saml.SamlLogoutSession BuildLogoutSession(string logoutId, DateTime expiresAtUtc) => new()
    {
        LogoutId = logoutId,
        ExpectedResponses = new Dictionary<string, ExpectedSpLogout>
        {
            [$"req-{Guid.NewGuid():N}"] = new("https://sp1.example.com")
        },
        CreatedUtc = DateTimeOffset.UtcNow,
        ExpiresAtUtc = expiresAtUtc
    };

    private async Task<Duende.Storage.Internal.Operations.StoreGetResult> TryReadDeviceCodeAsync(string deviceCode)
    {
        var storage = await _fixture.PartitionedStorageFactory.GetPartitionedStorageAsync(DataCategoryName.Operational, _ct);
        return await storage.TryReadAsync(
            DeviceFlowDso.EntityType,
            Duende.Storage.Internal.DataStorageKey.Create(DeviceCodeDskV1.Create(deviceCode)),
            _ct);
    }

    private async Task<Duende.Storage.Internal.Operations.StoreGetResult> TryReadPushedAuthorizationAsync(string referenceValueHash)
    {
        var storage = await _fixture.PartitionedStorageFactory.GetPartitionedStorageAsync(DataCategoryName.Operational, _ct);
        return await storage.TryReadAsync(
            PushedAuthorizationDso.EntityType,
            Duende.Storage.Internal.DataStorageKey.Create(ReferenceValueHashDskV1.Create(referenceValueHash)),
            _ct);
    }

    private async Task<Duende.Storage.Internal.Operations.StoreGetResult> TryReadByIdAsync(Duende.Storage.Internal.EntityType entityType, Guid id)
    {
        var storage = await _fixture.PartitionedStorageFactory.GetPartitionedStorageAsync(DataCategoryName.Operational, _ct);
        return await storage.TryReadAsync(entityType, Duende.Storage.UuidV7.From(id), _ct);
    }

    private async Task<Duende.Storage.Internal.Operations.StoreGetResult> TryReadLogoutSessionAsync(string logoutId)
    {
        var storage = await _fixture.PartitionedStorageFactory.GetPartitionedStorageAsync(DataCategoryName.Operational, _ct);
        return await storage.TryReadAsync(
            SamlLogoutSessionDso.EntityType,
            Duende.Storage.Internal.DataStorageKey.Create(SamlLogoutSessionLogoutIdDskV1.Create(logoutId)),
            _ct);
    }

    private StoragePurgeHost CreatePurgeHost() => new(
        _fixture.StorageInstanceRouter,
        _fixture.CrossPartitionStorageFactory,
        _fixture.Options,
        new InProcessDistributedLock(),
        _fixture.Clock,
        _fixture.StoragePurgeHostLogger);
}
