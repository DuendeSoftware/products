// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Admin;
using Duende.IdentityServer.Admin.ApiResources;
using Duende.IdentityServer.Admin.ApiScopes;
using Duende.IdentityServer.Admin.Clients;
using Duende.IdentityServer.Admin.IdentityProviders;
using Duende.IdentityServer.Admin.IdentityResources;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores.Storage.IdentityProviders;
using Duende.Spaces;
using Duende.IdentityServer.DataExtensions;
using Duende.Storage.Schema;
using Duende.Storage.Sqlite;
using Duende.UserManagement.Profiles;
using StorageInstanceId = Duende.Storage.StorageInstanceId;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.IdentityServer.OutsideIn.Harness.Internal.Storage;

/// <summary>
/// The Duende.Storage store type in its split layout (design doc <c>DuendeStorageStoreProvider</c>, D29): configuration
/// data in one SQLite storage instance, operational data in another, both shared by every node. Schemas (data extension
/// schemas, D32) either stay in memory or are stored in the configuration database. Writes go through IdentityServer's
/// public admin API. Caching (design doc D34) wraps the configuration stores (see <see cref="CachingDecoration" />):
/// with <see cref="CachingMode.InMemory" /> each node gets its own cache; with <see cref="CachingMode.Distributed" />
/// both nodes share one <see cref="IDistributedCache" /> instance.
/// </summary>
internal sealed class DuendeStorageStoreProvider : DatabaseStoreProvider
{
    private static readonly StorageInstanceId ConfigurationInstance = StorageInstanceId.Create("configuration");
    private static readonly StorageInstanceId OperationalInstance = StorageInstanceId.Create("operational");

    private readonly bool _schemasInDatabase;
    private readonly bool _spacesOn;
    private readonly bool _usersOn;
    private readonly CachingMode _caching;
    private readonly IDistributedCache? _distributedCache;
    private readonly TestDatabaseDirectory _directory = new();
    private readonly TestDatabase _configuration;
    private readonly TestDatabase _operational;

    public DuendeStorageStoreProvider(bool schemasInDatabase, bool spacesOn, bool usersOn, CachingMode caching, IDistributedCache? distributedCache)
    {
        _schemasInDatabase = schemasInDatabase;
        _spacesOn = spacesOn;
        _usersOn = usersOn;
        _caching = caching;
        _distributedCache = distributedCache;
        _configuration = _directory.Database("configuration");
        _operational = _directory.Database("operational");
    }

    public override void ConfigureNode(IIdentityServerBuilder builder, int nodeIndex)
    {
        builder
            .AddStorage(ConfigurationInstance, storage => storage.AddSqlite(options => options.ConnectionString = _configuration.ConnectionString))
            .AddStorage(OperationalInstance, storage => storage.AddSqlite(options => options.ConnectionString = _operational.ConnectionString))
            .AddConfigurationStorage(ConfigurationInstance)
            .AddOperationalStorage(OperationalInstance);

        if (_caching != CachingMode.None)
        {
            builder.Services.UseCacheTiers(_distributedCache);
            builder.AddCachingOverRegisteredStores();
        }

        if (_schemasInDatabase)
        {
            builder.AddDynamicSchemas(ConfigurationInstance);
        }

        if (_spacesOn)
        {
            builder.AddSpaces(ConfigurationInstance, spaces => spaces.FallbackToDefault = false);
        }

        if (_usersOn)
        {
            // D20: with User Management on, users and sign-in go through it only, never the test users. It shares the
            // topology's configuration database (D6), so it's visible to both nodes and, with Spaces on, scoped per space.
            // AddUserManagement now also registers the default user profile schema and replaces IProfileService
            // itself, so the harness no longer needs to do either by hand.
            builder.AddUserManagement(ConfigurationInstance, _ => { });
        }

        if (_spacesOn || _usersOn)
        {
            SpacesTestLicense.RegisterEntitled(builder.Services);
        }

        // The background purge runs on the wall clock and would race the test; nothing in a test needs it yet.
        builder.Services.Configure<IdentityServerOptions>(options => options.StoragePurge.EnablePurge = false);
    }

    /// <summary>
    /// Creates a user in User Management (D20), in <paramref name="spaceId" /> if any. Goes through the store
    /// provider's own scope, like every other write here, so it works as soon as this store has initialized (D3),
    /// not only once every node in the topology has started.
    /// </summary>
    public Task AddUserManagementUserAsync(TestUser user, SpaceId? spaceId, Ct cancellationToken) =>
        InScopeAsync(async services =>
        {
            if (spaceId is null)
            {
                await UserManagementProvisioning.AddUserAsync(services, user, cancellationToken);
                return;
            }

            var accessor = services.GetRequiredService<ISpaceContextAccessor>();
            using (accessor.SetSpace(spaceId))
            {
                await UserManagementProvisioning.AddUserAsync(services, user, cancellationToken);
            }
        });

    public override Task AddClientAsync(Client client, Ct cancellationToken) =>
        InScopeAsync(async services =>
            Check(await services.GetRequiredService<IClientAdmin>().CreateAsync(ToCreate(client), cancellationToken), $"client {client.ClientId}"));

    public override async Task UpdateClientAsync(Client client, Ct cancellationToken)
    {
        await RemoveClientAsync(client.ClientId, cancellationToken);
        await AddClientAsync(client, cancellationToken);
    }

    public override Task RemoveClientAsync(string clientId, Ct cancellationToken) =>
        InScopeAsync(async services =>
        {
            var admin = services.GetRequiredService<IClientAdmin>();
            var clients = await admin.QueryAsync(new QueryRequest<ClientFilter, ClientSortField>(), cancellationToken);
            if (clients.Results.FirstOrDefault(item => item.ClientId == clientId) is { } existing)
            {
                Check(await admin.DeleteAsync(existing.Id, cancellationToken), $"client {clientId}");
            }
        });

    public override Task AddIdentityResourceAsync(IdentityResource resource, Ct cancellationToken) =>
        InScopeAsync(async services => Check(
            await services.GetRequiredService<IIdentityResourceAdmin>().CreateAsync(
                new CreateIdentityResource
                {
                    Name = resource.Name,
                    Enabled = resource.Enabled,
                    DisplayName = resource.DisplayName,
                    Description = resource.Description,
                    ShowInDiscoveryDocument = resource.ShowInDiscoveryDocument,
                    Required = resource.Required,
                    Emphasize = resource.Emphasize,
                    UserClaims = [.. resource.UserClaims],
                },
                cancellationToken),
            $"identity resource {resource.Name}"));

    public override Task<bool> HasIdentityResourceAsync(string name, Ct cancellationToken) =>
        InScopeAsync(async services =>
            (await services.GetRequiredService<IIdentityResourceAdmin>().GetByNameAsync(name, cancellationToken)).Found);

    public override Task AddApiScopeAsync(ApiScope scope, Ct cancellationToken) =>
        InScopeAsync(async services => Check(
            await services.GetRequiredService<IApiScopeAdmin>().CreateAsync(
                new CreateApiScope
                {
                    Name = scope.Name,
                    Enabled = scope.Enabled,
                    DisplayName = scope.DisplayName,
                    Description = scope.Description,
                    ShowInDiscoveryDocument = scope.ShowInDiscoveryDocument,
                    Required = scope.Required,
                    Emphasize = scope.Emphasize,
                    UserClaims = [.. scope.UserClaims],
                },
                cancellationToken),
            $"API scope {scope.Name}"));

    public override Task AddApiResourceAsync(ApiResource resource, Ct cancellationToken) =>
        InScopeAsync(async services =>
        {
            var admin = services.GetRequiredService<IApiResourceAdmin>();
            var id = Check(
                await admin.CreateAsync(
                    new CreateApiResource
                    {
                        Name = resource.Name,
                        Enabled = resource.Enabled,
                        DisplayName = resource.DisplayName,
                        Description = resource.Description,
                        ShowInDiscoveryDocument = resource.ShowInDiscoveryDocument,
                        RequireResourceIndicator = resource.RequireResourceIndicator,
                        UserClaims = [.. resource.UserClaims],
                        Scopes = [.. resource.Scopes],
                        AllowedAccessTokenSigningAlgorithms = [.. resource.AllowedAccessTokenSigningAlgorithms],
                    },
                    cancellationToken),
                $"API resource {resource.Name}");

            // The admin API hashes secrets itself, so it needs the plaintext the harness generated (see TestSecrets).
            foreach (var secret in resource.ApiSecrets)
            {
                var plaintext = TestSecrets.PlaintextOf(secret)
                    ?? throw new NotSupportedException(
                        "Duende.Storage's admin API takes plaintext API resource secrets, and this secret was hashed outside the harness. " +
                        "Create API resource secrets with the harness's test data builders.");
                Check(
                    await admin.CreateSecretAsync(id, plaintext, hashAlgorithm: null, secret.Description, secret.Expiration, type: null, cancellationToken),
                    $"API resource secret for {resource.Name}");
            }
        });

    public override Task AddIdentityProviderAsync(OidcProvider provider, Ct cancellationToken)
    {
        var create = new CreateIdentityProvider
        {
            Scheme = provider.Scheme,
            DisplayName = provider.DisplayName,
            Enabled = provider.Enabled,
            Type = provider.Type,
        };
        foreach (var (name, value) in provider.Properties)
        {
            create.ExtendedProperties.Set(AttributeCode.Create(name), value);
        }

        return InScopeAsync(async services => Check(
            await services.GetRequiredService<IIdentityProviderAdmin>().CreateAsync(create, cancellationToken),
            $"identity provider {provider.Scheme}"));
    }

    public override ValueTask DisposeAsync()
    {
        _directory.Dispose();
        return ValueTask.CompletedTask;
    }

    protected override async Task CreateSchemaAsync(IServiceProvider scopedServices, HarnessLog log, Ct cancellationToken)
    {
        var schemas = scopedServices.GetRequiredService<IStorageInstanceSchemaFactory>();
        foreach (var (instance, database) in new[] { (ConfigurationInstance, _configuration), (OperationalInstance, _operational) })
        {
            using (log.Measure($"Duende.Storage migration for {database.Name} database"))
            {
                var schema = await schemas.GetStorageInstanceSchema(instance, cancellationToken);
                await schema.MigrateAsync(cancellationToken);
            }
        }

        if (_schemasInDatabase)
        {
            using (log.Measure("Duende.Storage built-in OIDC provider schema"))
            {
                await AddBuiltInOidcProviderSchemaAsync(scopedServices, cancellationToken);
            }

            if (_usersOn)
            {
                using (log.Measure("Duende.Storage built-in user profile schema"))
                {
                    await AddBuiltInUserProfileSchemaAsync(scopedServices, cancellationToken);
                }
            }
        }
    }

    /// <summary>
    /// Prepares a new space, called with that space set. Stored schemas belong to one space, so each space needs its
    /// own copy of the built-in OIDC provider schema.
    /// </summary>
    public async Task InitializeSpaceAsync(Ct cancellationToken)
    {
        if (_schemasInDatabase)
        {
            await InScopeAsync(services => AddBuiltInOidcProviderSchemaAsync(services, cancellationToken));
            if (_usersOn)
            {
                await InScopeAsync(services => AddBuiltInUserProfileSchemaAsync(services, cancellationToken));
            }
        }
    }

    /// <summary>Dynamic schemas don't seed the built-in identity provider schema, so OIDC providers have nothing to validate against.</summary>
    private static async Task AddBuiltInOidcProviderSchemaAsync(IServiceProvider scopedServices, Ct cancellationToken) =>
        Check(
            await scopedServices.GetRequiredService<ISchemaAdmin>().CreateAsync(DefaultOidcProviderSchema.CreateSchema(), cancellationToken),
            "the built-in OIDC identity provider schema");

    /// <summary>Dynamic schemas don't seed User Management's built-in user profile schema either (D32).</summary>
    private static async Task AddBuiltInUserProfileSchemaAsync(IServiceProvider scopedServices, Ct cancellationToken) =>
        Check(
            await scopedServices.GetRequiredService<ISchemaAdmin>().CreateAsync(BuiltInSchemas.UserProfile, cancellationToken),
            "the built-in user profile schema");

    private static CreateClient ToCreate(Client client) =>
        new()
        {
            ClientId = client.ClientId,
            Enabled = client.Enabled,
            ClientName = client.ClientName,
            Description = client.Description,
            ClientUri = client.ClientUri,
            LogoUri = client.LogoUri,
            RequireClientSecret = client.RequireClientSecret,
            RequirePkce = client.RequirePkce,
            AllowPlainTextPkce = client.AllowPlainTextPkce,
            RequireRequestObject = client.RequireRequestObject,
            RequireDPoP = client.RequireDPoP,
            DPoPValidationMode = client.DPoPValidationMode,
            DPoPClockSkew = client.DPoPClockSkew,
            RequireConsent = client.RequireConsent,
            AllowRememberConsent = client.AllowRememberConsent,
            ConsentLifetime = client.ConsentLifetime,
            AllowAccessTokensViaBrowser = client.AllowAccessTokensViaBrowser,
            AllowOfflineAccess = client.AllowOfflineAccess,
            AccessTokenType = client.AccessTokenType,
            IncludeJwtId = client.IncludeJwtId,
            IdentityTokenLifetime = client.IdentityTokenLifetime,
            AccessTokenLifetime = client.AccessTokenLifetime,
            AuthorizationCodeLifetime = client.AuthorizationCodeLifetime,
            AbsoluteRefreshTokenLifetime = client.AbsoluteRefreshTokenLifetime,
            SlidingRefreshTokenLifetime = client.SlidingRefreshTokenLifetime,
            RefreshTokenUsage = client.RefreshTokenUsage,
            RefreshTokenExpiration = client.RefreshTokenExpiration,
            UpdateAccessTokenClaimsOnRefresh = client.UpdateAccessTokenClaimsOnRefresh,
            AlwaysIncludeUserClaimsInIdToken = client.AlwaysIncludeUserClaimsInIdToken,
            AlwaysSendClientClaims = client.AlwaysSendClientClaims,
            ClientClaimsPrefix = client.ClientClaimsPrefix,
            PairWiseSubjectSalt = client.PairWiseSubjectSalt,
            UserSsoLifetime = client.UserSsoLifetime,
            CoordinateLifetimeWithUserSession = client.CoordinateLifetimeWithUserSession,
            EnableLocalLogin = client.EnableLocalLogin,
            FrontChannelLogoutUri = client.FrontChannelLogoutUri,
            FrontChannelLogoutSessionRequired = client.FrontChannelLogoutSessionRequired,
            BackChannelLogoutUri = client.BackChannelLogoutUri,
            BackChannelLogoutSessionRequired = client.BackChannelLogoutSessionRequired,
            InitiateLoginUri = client.InitiateLoginUri,
            RequirePushedAuthorization = client.RequirePushedAuthorization,
            PushedAuthorizationLifetime = client.PushedAuthorizationLifetime,
            UserCodeType = client.UserCodeType,
            DeviceCodeLifetime = client.DeviceCodeLifetime,
            CibaLifetime = client.CibaLifetime,
            PollingInterval = client.PollingInterval,
            AllowedGrantTypes = [.. client.AllowedGrantTypes],
            // The Admin API models offline_access as AllowOfflineAccess (mapped above), not as an allowed scope,
            // and refuses it in AllowedScopes.
            AllowedScopes = [.. client.AllowedScopes.Where(s => s != IdentityServerConstants.StandardScopes.OfflineAccess)],
            RedirectUris = [.. client.RedirectUris],
            PostLogoutRedirectUris = [.. client.PostLogoutRedirectUris],
            AllowedIdentityTokenSigningAlgorithms = [.. client.AllowedIdentityTokenSigningAlgorithms],
            IdentityProviderRestrictions = [.. client.IdentityProviderRestrictions],
            AllowedCorsOrigins = [.. client.AllowedCorsOrigins],
            ClientSecrets = [.. client.ClientSecrets.Select(ToCreate)],
        };

    // The admin API hashes secrets itself, so it needs the plaintext the harness generated (see TestSecrets).
    private static CreateClientSecret ToCreate(Secret secret) =>
        new()
        {
            PlaintextValue = TestSecrets.PlaintextOf(secret)
                ?? throw new NotSupportedException(
                    "Duende.Storage's admin API takes plaintext client secrets, and this secret was hashed outside the harness. " +
                    "Create client secrets with the harness's test data builders."),
            Description = secret.Description,
            Expiration = secret.Expiration,
        };

    private static TId Check<TId>(Duende.IdentityServer.Models.SaveResult<TId> result, string what)
        where TId : notnull
    {
        if (!result.IsSuccess)
        {
            throw new InvalidOperationException(
                $"Duende.Storage rejected {what}: {string.Join("; ", result.Errors?.Select(error => error.ToString()) ?? [])}");
        }

        return result.Id!;
    }

    private static TId Check<TId>(Duende.Storage.Internal.SaveResult<TId> result, string what)
        where TId : notnull
    {
        if (!result.IsSuccess)
        {
            throw new InvalidOperationException(
                $"Duende.Storage rejected {what}: {string.Join("; ", result.Errors?.Select(error => error.ToString()) ?? [])}");
        }

        return result.Id;
    }
}
