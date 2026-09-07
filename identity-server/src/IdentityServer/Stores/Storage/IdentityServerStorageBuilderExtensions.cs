// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Admin;
using Duende.IdentityServer.Hosting.DynamicProviders;
using Duende.IdentityServer.Hosting.OutboxProcessor;
using Duende.IdentityServer.Saml;
using Duende.IdentityServer.Saml.Infrastructure;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Services.KeyManagement;
using Duende.IdentityServer.Stores;
using Duende.IdentityServer.Stores.Empty;
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
using Duende.Storage.EntityAttributeValue.Internal;
using Duende.Storage.Internal;
using Duende.Storage.Internal.Builder;
using Duende.Storage.Internal.Outbox;
using Duende.Storage.Schema;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Duende.IdentityServer;

/// <summary>
/// Extension methods for configuring Duende.Storage-based stores for IdentityServer.
/// </summary>
public static class IdentityServerStorageBuilderExtensions
{
    extension(IIdentityServerBuilder builder)
    {
        /// <summary>
        /// Adds the complete set of Duende.Storage-backed stores, admin services, and supporting
        /// infrastructure for IdentityServer configuration and operational data.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Registers configuration stores/admin services (clients, identity providers, SAML service
        /// providers, API/identity resources and scopes, CORS policy) and operational stores/admin
        /// infrastructure (persisted grants, device flow, pushed authorization requests, server-side
        /// sessions, signing keys, SAML signin state, SAML logout sessions), along with the shared
        /// pooled storage factory, outbox subscriber/processor, and background purge hosted services.
        /// </para>
        /// <para>
        /// This method does not select a physical database engine. Call a provider registration method
        /// such as <c>AddSqliteStore()</c>, <c>AddPostgreSqlStore()</c>, or <c>AddMsSqlStore()</c> to
        /// supply the underlying <c>IPooledStore</c>; resolving storage without one throws an actionable
        /// <see cref="InvalidOperationException"/>.
        /// </para>
        /// <para>
        /// The default <c>ISchemaStore</c> is an in-memory store seeded with the built-in identity
        /// provider schemas (<c>idp:oidc</c>, <c>idp:saml</c>) so that OIDC and SAML providers work
        /// without further configuration; no <c>ISchemaAdmin</c> is registered by default. Call
        /// <c>AddDynamicSchemas()</c> after this method to opt into a database-backed,
        /// mutable schema store and admin API instead; when doing so and starting from empty
        /// persistent storage, built-in OIDC/SAML schemas are not automatically seeded and must be
        /// created explicitly via <c>ISchemaAdmin</c> if extended-property validation against them is
        /// required. Call <c>AddInMemoryDataExtensionSchemas(schemas)</c> to add caller-supplied
        /// schemas alongside the built-ins while keeping them immutable at runtime.
        /// </para>
        /// <para>
        /// Explicit store selectors called after this method (for example
        /// <c>AddInMemoryClients()</c>, <c>AddInMemoryPersistedGrants()</c>) replace the corresponding
        /// Duende.Storage-backed registration made here.
        /// </para>
        /// </remarks>
        public IIdentityServerBuilder AddStorage(Action<IStorageBuilder> configure)
        {
            var services = builder.Services;

            // Make sure all storage is wired up
            services.AddStorageInternal(configure);
            AddStorageInfrastructure(services);

            // DSO type registrations (required for deserialization by the storage layer)
            services.AddDsoRegistration<ClientDso.V1>();
            services.AddDsoRegistration<IdentityProviderDso.V1>();
            services.AddDsoRegistration<SamlServiceProviderDso.V1>();
            services.AddDsoRegistration<ApiResourceDso.V1>();
            services.AddDsoRegistration<ApiScopeDso.V1>();
            services.AddDsoRegistration<IdentityResourceDso.V1>();
            services.AddDsoRegistration<PersistedGrantDso.V1>();
            services.AddDsoRegistration<DeviceFlowDso.V1>();
            services.AddDsoRegistration<KeyDso.V1>();
            services.AddDsoRegistration<PushedAuthorizationDso.V1>();
            services.AddDsoRegistration<SamlSigninStateDso.V1>();
            services.AddDsoRegistration<ServerSideSessionDso.V1>();
            services.AddDsoRegistration<SamlLogoutSessionDso.V1>();

            // Repository (scoped per-request lifetime matches store handle semantics)
            services.TryAddTransient<ClientRepository>();
            services.TryAddTransient<IdentityProviderRepository>();
            services.TryAddTransient<SamlServiceProviderRepository>();
            services.TryAddTransient<ApiResourceRepository>();
            services.TryAddTransient<ApiScopeRepository>();
            services.TryAddTransient<IdentityResourceRepository>();
            services.TryAddTransient<PersistedGrantRepository>();
            services.TryAddTransient<DeviceFlowRepository>();
            services.TryAddTransient<KeyRepository>();
            services.TryAddTransient<PushedAuthorizationRepository>();
            services.TryAddTransient<SamlSigninStateRepository>();
            services.TryAddTransient<ServerSideSessionRepository>();
            services.TryAddTransient<SamlLogoutSessionRepository>();

            services.TryAddTransientOrDefault<IClientStore, EmptyClientStore, ClientStore>();

            // Identity provider store
            services
                .TryAddTransientOrDefault<IIdentityProviderStore, NopIdentityProviderStore, IdentityProviderStore>();

            services
                .TryAddTransientOrDefault<ISamlServiceProviderStore, EmptySamlServiceProviderStore,
                    SamlServiceProviderStore>();

            // Resource store
            services.TryAddTransientOrDefault<IResourceStore, EmptyResourceStore, StorageResourceStore>();

            // CORS policy service
            services.TryAddTransientOrDefault<ICorsPolicyService, DefaultCorsPolicyService, StorageCorsPolicyService>();

            // Admin API
            services.TryAddTransient<IClientAdmin, ClientAdmin>();
            services.TryAddTransient<IIdentityProviderAdmin, IdentityProviderAdmin>();
            services.TryAddTransient<ISamlServiceProviderAdmin, SamlServiceProviderAdmin>();
            services.TryAddTransient<IApiResourceAdmin, ApiResourceAdmin>();
            services.TryAddTransient<IApiScopeAdmin, ApiScopeAdmin>();
            services.TryAddTransient<IIdentityResourceAdmin, IdentityResourceAdmin>();

            // Persisted grant store (operational upsert semantics, TTL expiration)
            services.TryAddTransientOrDefault<IPersistedGrantStore, InMemoryPersistedGrantStore, PersistedGrantStore>();

            // Device flow store
            services.TryAddTransientOrDefault<IDeviceFlowStore, InMemoryDeviceFlowStore, DeviceFlowStore>();

            // Pushed authorization request store
            services
                .TryAddTransientOrDefault<IPushedAuthorizationRequestStore, InMemoryPushedAuthorizationRequestStore,
                    PushedAuthorizationStore>();

            // Server-side session store only resolves when IServerSideSessionsMarker is registered
            builder.AddServerSideSessionStore<ServerSideSessionStore>();

            // Session expiration handler (back-channel logout for storage users). TryAddSingleton
            // keeps repeated AddStorage() calls idempotent for the marker and subscriber; the
            // keyed handler registration uses TryAddKeyedTransient for the same reason.
            services.TryAddSingleton<IStorageBackedSessionsMarker, StorageBackedSessionsMarker>();
            services.TryAddSingleton<IOutboxSubscriber, SessionExpirationSubscriber>();
            services.TryAddKeyedTransient<IOutboxSubscriberHandler>(SessionExpirationSubscriber.Name.Value,
                (sp, _) => new PoolAwareOutboxHandler(
                    ActivatorUtilities.CreateInstance<SessionExpirationHandler>(sp),
                    sp.GetRequiredService<IPoolContextAccessor>()));

            // Signing key store
            services.TryAddTransientOrDefault<ISigningKeyStore, FileSystemKeyStore, SigningKeyStore>();

            // SAML signin state store
            services.TryAddTransientOrDefault<ISamlSigninStateStore, InMemorySamlSigninStateStore, SamlSigninStateStore>();
            services.TryAddSingleton<ISamlSigninStateSerializer, JsonSamlSigninStateSerializer>();

            // SAML logout session store
            services
                .TryAddTransientOrDefault<ISamlLogoutSessionStore, InMemorySamlLogoutSessionStore,
                    SamlLogoutSessionStore>();

            // Outbox processor and background purge hosted services. TryAddEnumerable keeps repeated
            // AddStorage() calls from registering duplicate IHostedService instances.
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, OutboxProcessorHost>());
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, Hosting.StoragePurgeHost>());

            return builder;
        }

        /// <summary>
        /// Replaces the default in-memory schema store with a database-backed, mutable schema store
        /// and registers the <c>ISchemaAdmin</c> API so data extension schemas can be created and
        /// modified at runtime. Call this after <c>AddStorage()</c>.
        /// </summary>
        public IIdentityServerBuilder AddDynamicSchemas()
        {
            builder.Services.AddDynamicSchemaStorage();
            return builder;
        }
    }

    private static void AddStorageInfrastructure(IServiceCollection services)
    {
        // Storage infrastructure (idempotent, safe to call from both methods)
        services.TryAddSingleton<IPooledStore>(_ => throw new InvalidOperationException(
            "No storage provider has been configured for IdentityServer. " +
            "Call a storage registration method such as AddSqliteStore(), " +
            "AddPostgreSqlStore(), or AddMsSqlStore()."));

        services.TryAddSingleton<IPoolContextAccessor, PoolContextAccessor>();
        services.TryAddSingleton<IStorageFactory, DefaultStorageFactory>();

        // Default schema store seeded with the built-in identity provider schemas.
        // This ensures OIDC and SAML providers work without requiring explicit schema registration.
        // Calling AddInMemoryDataExtensionSchemas() or AddDynamicSchemas() will replace this.
        services.TryAddSingleton<Storage.EntityAttributeValue.ISchemaStore>(
            new Storage.EntityAttributeValue.InMemorySchemaStore(
                BuiltInSchemas.All));
    }


}
