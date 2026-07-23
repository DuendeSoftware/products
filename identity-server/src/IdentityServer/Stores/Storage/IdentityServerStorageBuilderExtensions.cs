// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Admin;
using Duende.IdentityServer.Saml;
using Duende.IdentityServer.Stores.Storage;
using Duende.IdentityServer.Stores.Storage.ApiResources;
using Duende.IdentityServer.Stores.Storage.ApiScopes;
using Duende.IdentityServer.Stores.Storage.Clients;
using Duende.IdentityServer.Stores.Storage.DeviceFlow;
using Duende.IdentityServer.Stores.Storage.IdentityProviders;
using Duende.IdentityServer.Stores.Storage.IdentityResources;
using Duende.IdentityServer.Stores.Storage.PersistedGrants;
using Duende.IdentityServer.Stores.Storage.PushedAuthorization;
using Duende.IdentityServer.Stores.Storage.Resources;
using Duende.IdentityServer.Stores.Storage.SamlLogoutSession;
using Duende.IdentityServer.Stores.Storage.SamlServiceProviders;
using Duende.IdentityServer.Stores.Storage.SamlSigninState;
using Duende.IdentityServer.Stores.Storage.ServerSideSessions;
using Duende.IdentityServer.Stores.Storage.SigningKeys;
using Duende.Storage.Internal;
using Duende.Storage.Internal.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Duende.IdentityServer;

/// <summary>
/// Extension methods for configuring IStore-based stores for IdentityServer.
/// </summary>
public static class IdentityServerStorageBuilderExtensions
{
    /// <summary>
    /// Adds the IStore-based configuration stores (clients, identity providers, SAML service providers, resources, scopes).
    /// Registers IClientStore, IClientAdmin, IIdentityProviderStore, IIdentityProviderAdmin,
    /// ISamlServiceProviderStore, ISamlServiceProviderAdmin, IApiResourceAdmin, IApiScopeAdmin,
    /// IIdentityResourceAdmin, IResourceStore, and ICorsPolicyService.
    /// </summary>
    public static IIdentityServerBuilder AddConfigurationStorage(
        this IIdentityServerBuilder builder)
    {
        var services = builder.Services;

        AddStorageInfrastructure(services);

        // DSO type registrations (required for deserialization by the storage layer)
        services.AddDsoRegistration<ClientDso.V1>();
        services.AddDsoRegistration<IdentityProviderDso.V1>();
        services.AddDsoRegistration<SamlServiceProviderDso.V1>();
        services.AddDsoRegistration<ApiResourceDso.V1>();
        services.AddDsoRegistration<ApiScopeDso.V1>();
        services.AddDsoRegistration<IdentityResourceDso.V1>();

        // Repository (scoped — per-request lifetime matches store handle semantics)
        services.AddScoped<ClientRepository>();
        services.AddScoped<IdentityProviderRepository>();
        services.AddScoped<SamlServiceProviderRepository>();
        services.AddScoped<ApiResourceRepository>();
        services.AddScoped<ApiScopeRepository>();
        services.AddScoped<IdentityResourceRepository>();

        // Client store — AddClientStore<T>() automatically wraps in ValidatingClientStore<T>
        // which fires InvalidClientConfigurationEvent and logs validation failures at runtime
        builder.AddClientStore<ClientStore>();

        // Identity provider store
        builder.AddIdentityProviderStore<IdentityProviderStore>();

        // SAML Service Provider store — AddSamlServiceProviderStore<T>() wraps in
        // ValidatingSamlServiceProviderStore<T> and registers the configuration validator
        builder.AddSamlServiceProviderStore<SamlServiceProviderStore>();

        // Resource store
        builder.AddResourceStore<StorageResourceStore>();

        // CORS policy service
        builder.AddCorsPolicyService<StorageCorsPolicyService>();

        // Admin API
        services.AddScoped<IClientAdmin, ClientAdmin>();
        services.AddScoped<IIdentityProviderAdmin, IdentityProviderAdmin>();
        services.AddScoped<ISamlServiceProviderAdmin, SamlServiceProviderAdmin>();
        services.AddScoped<IApiResourceAdmin, ApiResourceAdmin>();
        services.AddScoped<IApiScopeAdmin, ApiScopeAdmin>();
        services.AddScoped<IIdentityResourceAdmin, IdentityResourceAdmin>();

        return builder;
    }

    /// <summary>
    /// Adds the IStore-based operational stores (persisted grants, device flow, pushed authorization,
    /// server-side sessions, signing keys, SAML signin state, and SAML logout sessions).
    /// Registers IPersistedGrantStore, IDeviceFlowStore, IPushedAuthorizationRequestStore,
    /// IServerSideSessionStore (conditional — only resolves when AddServerSideSessions() has been called),
    /// ISigningKeyStore, ISamlSigninStateStore, and ISamlLogoutSessionStore.
    /// </summary>
    public static IIdentityServerBuilder AddOperationalStorage(
        this IIdentityServerBuilder builder)
    {
        var services = builder.Services;

        AddStorageInfrastructure(services);

        // DSO type registrations (required for deserialization by the storage layer)
        services.AddDsoRegistration<PersistedGrantDso.V1>();
        services.AddDsoRegistration<DeviceFlowDso.V1>();
        services.AddDsoRegistration<KeyDso.V1>();
        services.AddDsoRegistration<PushedAuthorizationDso.V1>();
        services.AddDsoRegistration<SamlSigninStateDso.V1>();
        services.AddDsoRegistration<ServerSideSessionDso.V1>();
        services.AddDsoRegistration<SamlLogoutSessionDso.V1>();

        // Repository (scoped — per-request lifetime matches store handle semantics)
        services.AddScoped<PersistedGrantRepository>();
        services.AddScoped<DeviceFlowRepository>();
        services.AddScoped<KeyRepository>();
        services.AddScoped<PushedAuthorizationRepository>();
        services.AddScoped<SamlSigninStateRepository>();
        services.AddScoped<ServerSideSessionRepository>();
        services.AddScoped<SamlLogoutSessionRepository>();

        // Persisted grant store (operational — upsert semantics, TTL expiration)
        builder.AddPersistedGrantStore<PersistedGrantStore>();

        // Device flow store
        builder.AddDeviceFlowStore<DeviceFlowStore>();

        // Pushed authorization request store
        builder.AddPushedAuthorizationRequestStore<PushedAuthorizationStore>();

        // Server-side session store — only resolves when IServerSideSessionsMarker is registered
        builder.AddServerSideSessionStore<ServerSideSessionStore>();

        // Signing key store
        builder.AddSigningKeyStore<SigningKeyStore>();

        // SAML signin state store
        builder.AddSamlSigninStateStore<SamlSigninStateStore>();
        services.TryAddSingleton<ISamlSigninStateSerializer, JsonSamlSigninStateSerializer>();

        // SAML logout session store
        builder.AddSamlLogoutSessionStore<SamlLogoutSessionStore>();

        // Background purge of expired entities
        services.AddSingleton<IHostedService, Hosting.StoragePurgeHost>();

        return builder;
    }

    private static void AddStorageInfrastructure(IServiceCollection services)
    {
        // Storage infrastructure (idempotent — safe to call from both methods)
        services.TryAddSingleton<IPooledStore>(_ => throw new InvalidOperationException(
            "No storage provider has been configured for IdentityServer. " +
            "Call a storage registration method such as AddSqliteStore(), " +
            "AddPostgreSqlStore(), or AddMsSqlStore()."));

        services.TryAddSingleton<IStoreFactory, DefaultStoreFactory>();

        // Default empty schema store so ClientAdmin resolves without requiring
        // explicit AddInMemoryDataExtensionSchemas / AddStorageDataExtensionSchemas.
        services.TryAddSingleton<Duende.Storage.EntityAttributeValue.ISchemaStore>(
            new Duende.Storage.EntityAttributeValue.InMemorySchemaStore([]));
    }
}
