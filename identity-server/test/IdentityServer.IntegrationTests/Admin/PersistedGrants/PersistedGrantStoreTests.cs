// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.IdentityServer.IntegrationTests.Admin.PersistedGrants;

/// <summary>
/// Integration tests for IPersistedGrantStore backed by IStore storage.
/// Covers upsert semantics, filter-based queries, batch deletes, and round-trip fidelity.
/// </summary>
public sealed class PersistedGrantStoreTests : IAsyncLifetime
{
    private readonly StorageTestFixture _fixture = new();
    private readonly Ct _ct = TestContext.Current.CancellationToken;
    private readonly List<IServiceScope> _scopes = [];

    private IPersistedGrantStore NewStore()
    {
        var scope = _fixture.CreateScope();
        _scopes.Add(scope);
        return scope.ServiceProvider.GetRequiredService<IPersistedGrantStore>();
    }

    // === Helper ===

    private static PersistedGrant CreateGrant(
        string? key = null,
        string? subjectId = null,
        string? clientId = null,
        string? type = null,
        string? sessionId = null,
        DateTime? expiration = null,
        string? data = null) => new()
        {
            Key = key ?? Guid.NewGuid().ToString("N"),
            SubjectId = subjectId ?? "sub_default",
            ClientId = clientId ?? "client_default",
            Type = type ?? "authorization_code",
            SessionId = sessionId,
            Description = "test grant",
            CreationTime = DateTime.UtcNow,
            Expiration = expiration,
            ConsumedTime = null,
            Data = data ?? "{\"test\":true}"
        };

    // === Round-trip ===

    [Fact]
    public async Task store_and_get_round_trips_all_properties()
    {
        var store = NewStore();
        var expiration = DateTime.UtcNow.AddHours(1);
        var consumed = DateTime.UtcNow.AddMinutes(5);
        var creation = DateTime.UtcNow;

        var grant = new PersistedGrant
        {
            Key = Guid.NewGuid().ToString("N"),
            Type = "refresh_token",
            SubjectId = "sub_roundtrip",
            SessionId = "session_roundtrip",
            ClientId = "client_roundtrip",
            Description = "full round-trip test",
            CreationTime = creation,
            Expiration = expiration,
            ConsumedTime = consumed,
            Data = "{\"payload\":\"test\"}"
        };

        await store.StoreAsync(grant, _ct);

        var result = await NewStore().GetAsync(grant.Key, _ct);

        result.ShouldNotBeNull();
        result.Key.ShouldBe(grant.Key);
        result.Type.ShouldBe(grant.Type);
        result.SubjectId.ShouldBe(grant.SubjectId);
        result.SessionId.ShouldBe(grant.SessionId);
        result.ClientId.ShouldBe(grant.ClientId);
        result.Description.ShouldBe(grant.Description);
        result.CreationTime.ShouldBe(creation, tolerance: TimeSpan.FromMilliseconds(1));
        result.Expiration.ShouldNotBeNull();
        result.Expiration!.Value.ShouldBe(expiration, tolerance: TimeSpan.FromMilliseconds(1));
        result.ConsumedTime.ShouldNotBeNull();
        result.ConsumedTime!.Value.ShouldBe(consumed, tolerance: TimeSpan.FromMilliseconds(1));
        result.Data.ShouldBe(grant.Data);
    }

    [Fact]
    public async Task get_nonexistent_key_returns_null()
    {
        var store = NewStore();
        var result = await store.GetAsync(Guid.NewGuid().ToString("N"), _ct);
        result.ShouldBeNull();
    }

    // === Upsert ===

    [Fact]
    public async Task store_replaces_existing_grant_with_same_key()
    {
        var key = Guid.NewGuid().ToString("N");
        var original = CreateGrant(key: key, data: "{\"version\":1}");
        await NewStore().StoreAsync(original, _ct);

        var updated = CreateGrant(key: key, data: "{\"version\":2}");
        await NewStore().StoreAsync(updated, _ct);

        var result = await NewStore().GetAsync(key, _ct);
        result.ShouldNotBeNull();
        result.Data.ShouldBe("{\"version\":2}");
    }

    [Fact]
    public async Task store_updates_consumed_time()
    {
        var key = Guid.NewGuid().ToString("N");
        var grant = CreateGrant(key: key);
        grant.ConsumedTime = null;

        await NewStore().StoreAsync(grant, _ct);

        var consumed = DateTime.UtcNow;
        grant.ConsumedTime = consumed;
        await NewStore().StoreAsync(grant, _ct);

        var result = await NewStore().GetAsync(key, _ct);
        result.ShouldNotBeNull();
        result.ConsumedTime.ShouldNotBeNull();
        result.ConsumedTime!.Value.ShouldBe(consumed, tolerance: TimeSpan.FromMilliseconds(1));
    }

    // === Remove ===

    [Fact]
    public async Task remove_by_key_deletes_grant()
    {
        var key = Guid.NewGuid().ToString("N");
        await NewStore().StoreAsync(CreateGrant(key: key), _ct);

        await NewStore().RemoveAsync(key, _ct);

        var result = await NewStore().GetAsync(key, _ct);
        result.ShouldBeNull();
    }

    [Fact]
    public async Task remove_nonexistent_key_does_not_throw()
    {
        var store = NewStore();
        await store.RemoveAsync(Guid.NewGuid().ToString("N"), _ct);
    }

    // === GetAll with filters ===

    [Fact]
    public async Task get_all_by_subject_id()
    {
        var subjectId = $"sub_{Guid.NewGuid():N}";
        await NewStore().StoreAsync(CreateGrant(subjectId: subjectId), _ct);
        await NewStore().StoreAsync(CreateGrant(subjectId: subjectId), _ct);
        await NewStore().StoreAsync(CreateGrant(subjectId: "sub_other"), _ct);

        var result = await NewStore().GetAllAsync(new PersistedGrantFilter { SubjectId = subjectId }, _ct);
        result.Count.ShouldBe(2);
        result.ShouldAllBe(g => g.SubjectId == subjectId);
    }

    [Fact]
    public async Task get_all_by_client_id()
    {
        var clientId = $"client_{Guid.NewGuid():N}";
        await NewStore().StoreAsync(CreateGrant(clientId: clientId), _ct);
        await NewStore().StoreAsync(CreateGrant(clientId: clientId), _ct);
        await NewStore().StoreAsync(CreateGrant(clientId: "client_other"), _ct);

        var result = await NewStore().GetAllAsync(new PersistedGrantFilter { ClientId = clientId }, _ct);
        result.Count.ShouldBe(2);
        result.ShouldAllBe(g => g.ClientId == clientId);
    }

    [Fact]
    public async Task get_all_by_type()
    {
        var grantType = $"type_{Guid.NewGuid():N}";
        await NewStore().StoreAsync(CreateGrant(type: grantType), _ct);
        await NewStore().StoreAsync(CreateGrant(type: grantType), _ct);
        await NewStore().StoreAsync(CreateGrant(type: "type_other"), _ct);

        var result = await NewStore().GetAllAsync(new PersistedGrantFilter { Type = grantType }, _ct);
        result.Count.ShouldBe(2);
        result.ShouldAllBe(g => g.Type == grantType);
    }

    [Fact]
    public async Task get_all_by_session_id()
    {
        var sessionId = $"session_{Guid.NewGuid():N}";
        await NewStore().StoreAsync(CreateGrant(sessionId: sessionId), _ct);
        await NewStore().StoreAsync(CreateGrant(sessionId: sessionId), _ct);
        await NewStore().StoreAsync(CreateGrant(sessionId: "session_other"), _ct);

        var result = await NewStore().GetAllAsync(new PersistedGrantFilter { SessionId = sessionId }, _ct);
        result.Count.ShouldBe(2);
        result.ShouldAllBe(g => g.SessionId == sessionId);
    }

    [Fact]
    public async Task get_all_multiple_filters_uses_and()
    {
        var subjectId = $"sub_{Guid.NewGuid():N}";
        var clientId = $"client_{Guid.NewGuid():N}";

        // Matches both
        await NewStore().StoreAsync(CreateGrant(subjectId: subjectId, clientId: clientId), _ct);
        // Matches subject only
        await NewStore().StoreAsync(CreateGrant(subjectId: subjectId, clientId: "client_other"), _ct);
        // Matches client only
        await NewStore().StoreAsync(CreateGrant(subjectId: "sub_other", clientId: clientId), _ct);

        var result = await NewStore().GetAllAsync(
            new PersistedGrantFilter { SubjectId = subjectId, ClientId = clientId }, _ct);

        result.Count.ShouldBe(1);
        result.Single().SubjectId.ShouldBe(subjectId);
        result.Single().ClientId.ShouldBe(clientId);
    }

    [Fact]
    public async Task get_all_with_client_ids_collection()
    {
        var clientId1 = $"client_{Guid.NewGuid():N}";
        var clientId2 = $"client_{Guid.NewGuid():N}";

        await NewStore().StoreAsync(CreateGrant(clientId: clientId1), _ct);
        await NewStore().StoreAsync(CreateGrant(clientId: clientId2), _ct);
        await NewStore().StoreAsync(CreateGrant(clientId: "client_excluded"), _ct);

        var result = await NewStore().GetAllAsync(
            new PersistedGrantFilter { ClientIds = [clientId1, clientId2] }, _ct);

        result.Count.ShouldBe(2);
        result.ShouldContain(g => g.ClientId == clientId1);
        result.ShouldContain(g => g.ClientId == clientId2);
    }

    [Fact]
    public async Task get_all_with_types_collection()
    {
        var type1 = $"type_{Guid.NewGuid():N}";
        var type2 = $"type_{Guid.NewGuid():N}";

        await NewStore().StoreAsync(CreateGrant(type: type1), _ct);
        await NewStore().StoreAsync(CreateGrant(type: type2), _ct);
        await NewStore().StoreAsync(CreateGrant(type: "type_excluded"), _ct);

        var result = await NewStore().GetAllAsync(
            new PersistedGrantFilter { Types = [type1, type2] }, _ct);

        result.Count.ShouldBe(2);
        result.ShouldContain(g => g.Type == type1);
        result.ShouldContain(g => g.Type == type2);
    }

    [Fact]
    public async Task get_all_client_id_and_client_ids_merged()
    {
        var clientId1 = $"client_{Guid.NewGuid():N}";
        var clientId2 = $"client_{Guid.NewGuid():N}";

        await NewStore().StoreAsync(CreateGrant(clientId: clientId1), _ct);
        await NewStore().StoreAsync(CreateGrant(clientId: clientId2), _ct);
        await NewStore().StoreAsync(CreateGrant(clientId: "client_excluded"), _ct);

        // Both ClientId and ClientIds set — they should be merged
        var result = await NewStore().GetAllAsync(
            new PersistedGrantFilter { ClientId = clientId1, ClientIds = [clientId2] }, _ct);

        result.Count.ShouldBe(2);
        result.ShouldContain(g => g.ClientId == clientId1);
        result.ShouldContain(g => g.ClientId == clientId2);
    }

    [Fact]
    public async Task get_all_type_and_types_merged()
    {
        var type1 = $"type_{Guid.NewGuid():N}";
        var type2 = $"type_{Guid.NewGuid():N}";

        await NewStore().StoreAsync(CreateGrant(type: type1), _ct);
        await NewStore().StoreAsync(CreateGrant(type: type2), _ct);
        await NewStore().StoreAsync(CreateGrant(type: "type_excluded"), _ct);

        // Both Type and Types set — they should be merged
        var result = await NewStore().GetAllAsync(
            new PersistedGrantFilter { Type = type1, Types = [type2] }, _ct);

        result.Count.ShouldBe(2);
        result.ShouldContain(g => g.Type == type1);
        result.ShouldContain(g => g.Type == type2);
    }

    [Fact]
    public async Task get_all_with_no_filter_throws()
    {
        var store = NewStore();
        await Should.ThrowAsync<ArgumentException>(
            () => store.GetAllAsync(new PersistedGrantFilter(), _ct));
    }

    // === RemoveAll with filters ===

    [Fact]
    public async Task remove_all_by_subject_and_client()
    {
        var subjectId = $"sub_{Guid.NewGuid():N}";
        var clientId = $"client_{Guid.NewGuid():N}";

        var matchKey1 = Guid.NewGuid().ToString("N");
        var matchKey2 = Guid.NewGuid().ToString("N");
        var keepKey = Guid.NewGuid().ToString("N");

        await NewStore().StoreAsync(CreateGrant(key: matchKey1, subjectId: subjectId, clientId: clientId), _ct);
        await NewStore().StoreAsync(CreateGrant(key: matchKey2, subjectId: subjectId, clientId: clientId), _ct);
        await NewStore().StoreAsync(CreateGrant(key: keepKey, subjectId: subjectId, clientId: "client_other"), _ct);

        await NewStore().RemoveAllAsync(
            new PersistedGrantFilter { SubjectId = subjectId, ClientId = clientId }, _ct);

        (await NewStore().GetAsync(matchKey1, _ct)).ShouldBeNull();
        (await NewStore().GetAsync(matchKey2, _ct)).ShouldBeNull();
        (await NewStore().GetAsync(keepKey, _ct)).ShouldNotBeNull();
    }

    [Fact]
    public async Task remove_all_by_type()
    {
        var grantType = $"type_{Guid.NewGuid():N}";
        var removeKey = Guid.NewGuid().ToString("N");
        var keepKey = Guid.NewGuid().ToString("N");

        await NewStore().StoreAsync(CreateGrant(key: removeKey, type: grantType), _ct);
        await NewStore().StoreAsync(CreateGrant(key: keepKey, type: "type_other"), _ct);

        await NewStore().RemoveAllAsync(new PersistedGrantFilter { Type = grantType }, _ct);

        (await NewStore().GetAsync(removeKey, _ct)).ShouldBeNull();
        (await NewStore().GetAsync(keepKey, _ct)).ShouldNotBeNull();
    }

    [Fact]
    public async Task remove_all_with_no_filter_throws()
    {
        var store = NewStore();
        await Should.ThrowAsync<ArgumentException>(
            () => store.RemoveAllAsync(new PersistedGrantFilter(), _ct));
    }

    // === Expiration ===

    [Fact]
    public async Task grant_with_expiration_round_trips()
    {
        var key = Guid.NewGuid().ToString("N");
        var expiration = DateTime.UtcNow.AddHours(2);
        var grant = CreateGrant(key: key, expiration: expiration);

        await NewStore().StoreAsync(grant, _ct);

        var result = await NewStore().GetAsync(key, _ct);
        result.ShouldNotBeNull();
        result.Expiration.ShouldNotBeNull();
        result.Expiration!.Value.ShouldBe(expiration, tolerance: TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public async Task grant_without_expiration_round_trips()
    {
        var key = Guid.NewGuid().ToString("N");
        var grant = CreateGrant(key: key, expiration: null);

        await NewStore().StoreAsync(grant, _ct);

        var result = await NewStore().GetAsync(key, _ct);
        result.ShouldNotBeNull();
        result.Expiration.ShouldBeNull();
    }

    // === Nullable fields ===

    [Fact]
    public async Task grant_with_null_subject_id_round_trips()
    {
        // Client credentials flows produce grants with no SubjectId
        var key = Guid.NewGuid().ToString("N");
        var grant = new PersistedGrant
        {
            Key = key,
            Type = "reference_token",
            SubjectId = null!,    // explicitly null — client credentials
            ClientId = "cc_client",
            SessionId = null,
            Description = null,
            CreationTime = DateTime.UtcNow,
            Expiration = null,
            ConsumedTime = null,
            Data = "{\"cc\":true}"
        };

        await NewStore().StoreAsync(grant, _ct);

        var result = await NewStore().GetAsync(key, _ct);
        result.ShouldNotBeNull();
        result.Key.ShouldBe(key);
        result.ClientId.ShouldBe("cc_client");
        result.Data.ShouldBe("{\"cc\":true}");
    }

    [Fact]
    public async Task grant_with_null_session_id_round_trips()
    {
        var key = Guid.NewGuid().ToString("N");
        var grant = CreateGrant(key: key, sessionId: null);

        await NewStore().StoreAsync(grant, _ct);

        var result = await NewStore().GetAsync(key, _ct);
        result.ShouldNotBeNull();
        result.SessionId.ShouldBeNull();
    }

    // === Lifecycle ===

    public ValueTask InitializeAsync() => _fixture.InitializeAsync();

    public async ValueTask DisposeAsync()
    {
        foreach (var scope in _scopes)
        {
            scope.Dispose();
        }

        await _fixture.DisposeAsync();
    }
}
