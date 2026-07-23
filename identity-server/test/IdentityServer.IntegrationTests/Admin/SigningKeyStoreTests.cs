// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;
using Duende.Storage;

namespace Duende.IdentityServer.IntegrationTests.Admin;

/// <summary>
/// Integration tests for the IStore-backed ISigningKeyStore implementation.
/// Tests cover all 3 interface methods against a real SQLite database.
/// </summary>
public sealed class SigningKeyStoreTests : IAsyncLifetime
{
    private readonly StorageTestFixture _fixture = new();
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    private ISigningKeyStore Store => _fixture.SigningKeyStore;

    private static SerializedKey BuildKey(string? id = null) =>
        new()
        {
            Id = id ?? $"key_{Guid.NewGuid():N}",
            Version = 1,
            Created = new DateTime(2024, 6, 15, 12, 0, 0, DateTimeKind.Utc),
            Algorithm = "RS256",
            IsX509Certificate = false,
            DataProtected = true,
            Data = $"serialized_key_data_{Guid.NewGuid():N}"
        };

    public async ValueTask InitializeAsync() => await _fixture.InitializeAsync();

    public async ValueTask DisposeAsync() => await _fixture.DisposeAsync();

    // ─────────────────────────── LoadKeys ───────────────────────────

    [Fact]
    public async Task LoadKeysAsync_WhenNoKeysExist_ReturnsEmptyCollection()
    {
        var results = await Store.LoadKeysAsync(_ct);

        results.ShouldBeEmpty();
    }

    [Fact]
    public async Task LoadKeysAsync_ReturnsAllStoredKeys()
    {
        var key1 = BuildKey();
        var key2 = BuildKey();

        await Store.StoreKeyAsync(key1, _ct);
        await Store.StoreKeyAsync(key2, _ct);

        var results = await Store.LoadKeysAsync(_ct);

        results.Count.ShouldBe(2);
        results.ShouldContain(k => k.Id == key1.Id);
        results.ShouldContain(k => k.Id == key2.Id);
    }

    [Fact]
    public async Task LoadKeysAsync_MapsAllFieldsCorrectly()
    {
        var key = new SerializedKey
        {
            Id = $"key_{Guid.NewGuid():N}",
            Version = 3,
            Created = new DateTime(2024, 3, 10, 8, 30, 0, DateTimeKind.Utc),
            Algorithm = "ES256",
            IsX509Certificate = true,
            DataProtected = false,
            Data = "test_key_material"
        };

        await Store.StoreKeyAsync(key, _ct);

        var results = await Store.LoadKeysAsync(_ct);
        var loaded = results.Single(k => k.Id == key.Id);

        loaded.Version.ShouldBe(3);
        loaded.Created.ShouldBe(key.Created);
        loaded.Algorithm.ShouldBe("ES256");
        loaded.IsX509Certificate.ShouldBeTrue();
        loaded.DataProtected.ShouldBeFalse();
        loaded.Data.ShouldBe("test_key_material");
    }

    // ─────────────────────────── StoreKey ───────────────────────────

    [Fact]
    public async Task StoreKeyAsync_PersistsKeyThatCanBeLoaded()
    {
        var key = BuildKey();

        await Store.StoreKeyAsync(key, _ct);
        var results = await Store.LoadKeysAsync(_ct);

        results.ShouldContain(k => k.Id == key.Id);
    }

    [Fact]
    public async Task StoreKeyAsync_WithDuplicateId_Throws()
    {
        var key = BuildKey();

        await Store.StoreKeyAsync(key, _ct);

        // Second store with same ID should throw (key conflict)
        await Should.ThrowAsync<InvalidOperationException>(() => Store.StoreKeyAsync(key, _ct));
    }

    // ─────────────────────────── DeleteKey ───────────────────────────

    [Fact]
    public async Task DeleteKeyAsync_RemovesKeyFromStore()
    {
        var key = BuildKey();
        await Store.StoreKeyAsync(key, _ct);

        await Store.DeleteKeyAsync(key.Id, _ct);

        var results = await Store.LoadKeysAsync(_ct);
        results.ShouldNotContain(k => k.Id == key.Id);
    }

    [Fact]
    public async Task DeleteKeyAsync_WhenKeyDoesNotExist_DoesNotThrow() =>
        await Should.NotThrowAsync(() =>
            Store.DeleteKeyAsync("nonexistent_key_id", _ct));

    [Fact]
    public async Task DeleteKeyAsync_DoesNotAffectOtherKeys()
    {
        var key1 = BuildKey();
        var key2 = BuildKey();
        await Store.StoreKeyAsync(key1, _ct);
        await Store.StoreKeyAsync(key2, _ct);

        await Store.DeleteKeyAsync(key1.Id, _ct);

        var results = await Store.LoadKeysAsync(_ct);
        results.ShouldNotContain(k => k.Id == key1.Id);
        results.ShouldContain(k => k.Id == key2.Id);
    }

    // ─────────────────────────── Use isolation ───────────────────────────

    [Fact]
    public async Task LoadKeysAsync_OnlyReturnsSigningKeys()
    {
        // Store a signing key via the normal store
        var signingKey = BuildKey();
        await Store.StoreKeyAsync(signingKey, _ct);

        // Insert a key with a different Use directly via the repository
        var otherKey = BuildKey("other_use_key");
        await _fixture.KeyRepository.CreateAsync(UuidV7.New(), otherKey, "encryption", _ct);

        // LoadKeysAsync should only return the signing key
        var results = await Store.LoadKeysAsync(_ct);
        results.ShouldContain(k => k.Id == signingKey.Id);
        results.ShouldNotContain(k => k.Id == otherKey.Id);
    }
}
