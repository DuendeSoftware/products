// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System.IdentityModel.Tokens.Jwt;
using Duende.IdentityModel.Client;
using Duende.IdentityServer.IntegrationTests.Common;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services.KeyManagement;
using Duende.IdentityServer.Stores;
using Duende.IdentityServer.Stores.Storage;
using Duende.IdentityServer.Stores.Storage.SigningKeys;
using Duende.Storage.Internal;
using Duende.Storage.Internal.Builder;
using Duende.Storage.Schema;
using Duende.Storage.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Duende.IdentityServer.IntegrationTests.Admin;

/// <summary>
/// Base class for KeyManager integration tests that validate key creation,
/// persistence, rotation, retirement, and token signing work correctly
/// regardless of store backend. Subclasses provide the store-specific DI configuration.
/// </summary>
public abstract class SigningKeyStoreKeyManagerTestsBase
{
    protected readonly IdentityServerPipeline Pipeline = new();
    protected readonly FakeTimeProvider FakeTime = new(DateTimeOffset.UtcNow);
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    protected SigningKeyStoreKeyManagerTestsBase()
    {
        Pipeline.Clients.Add(new Client
        {
            ClientId = "test_client",
            ClientSecrets = { new Secret("secret".Sha256()) },
            AllowedGrantTypes = GrantTypes.ClientCredentials,
            AllowedScopes = { "api" }
        });

        Pipeline.ApiScopes.Add(new ApiScope("api"));

        Pipeline.OnPreConfigureServices += services =>
        {
            services.AddSingleton<TimeProvider>(FakeTime);
        };

        Pipeline.OnPostConfigureServices += services =>
        {
            // Remove the developer signing credential so key management is the sole source
            services.RemoveAll<ISigningCredentialStore>();
            services.RemoveAll<IValidationKeysStore>();

            ConfigureKeyStore(services);
        };

        Pipeline.Initialize();

        // Enable key management (pipeline defaults to Enabled=false)
        Pipeline.Options.KeyManagement.Enabled = true;
        Pipeline.Options.KeyManagement.InitializationSynchronizationDelay = TimeSpan.FromMilliseconds(1);

        // Use short intervals so time-advancement tests are practical
        Pipeline.Options.KeyManagement.PropagationTime = TimeSpan.FromHours(1);
        Pipeline.Options.KeyManagement.RotationInterval = TimeSpan.FromHours(2);
        Pipeline.Options.KeyManagement.RetentionDuration = TimeSpan.FromHours(1);
        // KeyRetirementAge = RotationInterval + RetentionDuration = 3 hours
        Pipeline.Options.KeyManagement.KeyCacheDuration = TimeSpan.Zero;
    }

    /// <summary>
    /// Subclasses override this to register their ISigningKeyStore implementation.
    /// </summary>
    protected abstract void ConfigureKeyStore(IServiceCollection services);

    /// <summary>
    /// Hook for subclasses that require async initialization (e.g., schema migration).
    /// Default is a no-op.
    /// </summary>
    protected virtual Task InitializeStoreAsync() => Task.CompletedTask;

    private async Task<string> RequestTokenAsync()
    {
        var response = await Pipeline.BackChannelClient.RequestClientCredentialsTokenAsync(
            new ClientCredentialsTokenRequest
            {
                Address = IdentityServerPipeline.TokenEndpoint,
                ClientId = "test_client",
                ClientSecret = "secret",
                Scope = "api"
            },
            _ct);

        response.IsError.ShouldBeFalse(response.Error);
        response.AccessToken.ShouldNotBeNullOrEmpty();
        return response.AccessToken;
    }

    private async Task ClearKeyCacheAsync()
    {
        var cache = Pipeline.Resolve<ISigningKeyStoreCache>();
        await cache.StoreKeysAsync([], TimeSpan.Zero, _ct);
    }

    [Fact]
    public async Task TokenIssuance_WithKeyManager_CreatesAndPersistsSigningKey()
    {
        await InitializeStoreAsync();

        await RequestTokenAsync();

        var keyStore = Pipeline.Resolve<ISigningKeyStore>();
        var keys = await keyStore.LoadKeysAsync(_ct);
        keys.Count.ShouldBe(1);
        keys.First().Algorithm.ShouldBe("RS256");
    }

    [Fact]
    public async Task TokenIssuance_SubsequentRequests_ReuseExistingKey()
    {
        await InitializeStoreAsync();

        await RequestTokenAsync();

        var keyStore = Pipeline.Resolve<ISigningKeyStore>();
        var keysAfterFirst = await keyStore.LoadKeysAsync(_ct);
        keysAfterFirst.Count.ShouldBe(1);

        await RequestTokenAsync();

        var keysAfterSecond = await keyStore.LoadKeysAsync(_ct);
        keysAfterSecond.Count.ShouldBe(1);
        keysAfterSecond.First().Id.ShouldBe(keysAfterFirst.First().Id);
    }

    [Fact]
    public async Task TokenKid_MatchesStoredKeyId()
    {
        await InitializeStoreAsync();

        // Advance past propagation so key is active for signing
        FakeTime.Advance(TimeSpan.FromHours(1) + TimeSpan.FromMinutes(1));

        var accessToken = await RequestTokenAsync();

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(accessToken);
        var kid = jwt.Header.Kid;

        var keyStore = Pipeline.Resolve<ISigningKeyStore>();
        var keys = await keyStore.LoadKeysAsync(_ct);
        keys.ShouldContain(k => k.Id == kid);
    }

    [Fact]
    public async Task DiscoveryEndpoint_PublishesAutoManagedKey()
    {
        await InitializeStoreAsync();

        await RequestTokenAsync();

        var disco = await Pipeline.BackChannelClient.GetDiscoveryDocumentAsync(
            new DiscoveryDocumentRequest
            {
                Address = IdentityServerPipeline.DiscoveryEndpoint,
                Policy = new DiscoveryPolicy { RequireHttps = false }
            });

        disco.IsError.ShouldBeFalse(disco.Error);
        disco.KeySet.ShouldNotBeNull();
        disco.KeySet.Keys.Count.ShouldBe(1);
        disco.KeySet.Keys.First().Alg.ShouldBe("RS256");
    }

    [Fact]
    public async Task KeyRotation_CreatesNewKeyWhenExistingKeyAges()
    {
        await InitializeStoreAsync();

        // Create initial key
        await RequestTokenAsync();

        var keyStore = Pipeline.Resolve<ISigningKeyStore>();
        var initialKeys = await keyStore.LoadKeysAsync(_ct);
        initialKeys.Count.ShouldBe(1);
        var originalKeyId = initialKeys.First().Id;

        // Advance past RotationInterval (2 hours) so rotation is triggered
        FakeTime.Advance(TimeSpan.FromHours(2) + TimeSpan.FromMinutes(1));
        await ClearKeyCacheAsync();

        // Next request triggers rotation
        await RequestTokenAsync();

        var keysAfterRotation = await keyStore.LoadKeysAsync(_ct);
        keysAfterRotation.Count.ShouldBe(2);
        keysAfterRotation.ShouldContain(k => k.Id == originalKeyId);
        keysAfterRotation.ShouldContain(k => k.Id != originalKeyId);
    }

    [Fact]
    public async Task KeyRotation_BothKeysInJwksDuringRetentionWindow()
    {
        await InitializeStoreAsync();

        // Create initial key and advance past rotation
        await RequestTokenAsync();
        FakeTime.Advance(TimeSpan.FromHours(2) + TimeSpan.FromMinutes(1));
        await ClearKeyCacheAsync();

        // Trigger rotation
        await RequestTokenAsync();

        // Both keys should appear in JWKS
        var disco = await Pipeline.BackChannelClient.GetDiscoveryDocumentAsync(
            new DiscoveryDocumentRequest
            {
                Address = IdentityServerPipeline.DiscoveryEndpoint,
                Policy = new DiscoveryPolicy { RequireHttps = false }
            });

        disco.IsError.ShouldBeFalse(disco.Error);
        disco.KeySet.ShouldNotBeNull();
        disco.KeySet.Keys.Count.ShouldBe(2);
    }

    [Fact]
    public async Task KeyRetirement_DeletesOldKeyAfterRetirementAge()
    {
        await InitializeStoreAsync();

        // Create initial key
        await RequestTokenAsync();

        var keyStore = Pipeline.Resolve<ISigningKeyStore>();
        var initialKeys = await keyStore.LoadKeysAsync(_ct);
        var originalKeyId = initialKeys.First().Id;

        // Advance past KeyRetirementAge (RotationInterval + RetentionDuration = 3 hours)
        FakeTime.Advance(TimeSpan.FromHours(3) + TimeSpan.FromMinutes(1));
        await ClearKeyCacheAsync();

        // Next request triggers retirement cleanup and creates a new key
        await RequestTokenAsync();

        var keysAfterRetirement = await keyStore.LoadKeysAsync(_ct);
        keysAfterRetirement.ShouldNotContain(k => k.Id == originalKeyId);
    }
}

/// <summary>
/// Runs KeyManager tests with the default FileSystemKeyStore backend.
/// </summary>
public sealed class SigningKeyStoreKeyManagerTests_FileSystem : SigningKeyStoreKeyManagerTestsBase, IDisposable
{
    private readonly string _keyPath = Path.Combine(Path.GetTempPath(), $"is_keys_{Guid.NewGuid():N}");

    public SigningKeyStoreKeyManagerTests_FileSystem()
    {
        Directory.CreateDirectory(_keyPath);
        Pipeline.Options.KeyManagement.KeyPath = _keyPath;
    }

    protected override void ConfigureKeyStore(IServiceCollection services)
    {
        // No-op: uses the default FileSystemKeyStore registered by AddKeyManagement()
    }

    public void Dispose()
    {
        if (Directory.Exists(_keyPath))
        {
            Directory.Delete(_keyPath, recursive: true);
        }
    }
}

/// <summary>
/// Runs KeyManager tests with the IStore-backed SigningKeyStore backend.
/// </summary>
public sealed class SigningKeyStoreKeyManagerTests_IStore : SigningKeyStoreKeyManagerTestsBase
{
    private readonly string _dbName = $"keymgr_{Guid.NewGuid():N}";

    protected override void ConfigureKeyStore(IServiceCollection services)
    {
        services.AddStorageInternal(storage =>
            storage.AddSqliteStore(opt =>
                opt.ConnectionString = $"Data Source={_dbName};Mode=Memory;Cache=Shared"));

        services.AddDsoRegistration<KeyDso.V1>();
        services.AddSingleton<IStoreFactory, DefaultStoreFactory>();
        services.AddScoped<KeyRepository>();
        services.AddIdentityServerBuilder().AddSigningKeyStore<SigningKeyStore>();
    }

    protected override async Task InitializeStoreAsync()
    {
        var schema = Pipeline.Resolve<IDatabaseSchema>();
        await schema.MigrateAsync(TestContext.Current.CancellationToken);
    }
}
