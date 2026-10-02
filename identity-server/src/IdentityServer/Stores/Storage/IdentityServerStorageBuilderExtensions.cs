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
using Duende.Storage;
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
        /// Registers a Duende.Storage database for the given <paramref name="storageInstanceId"/>. The
        /// <paramref name="configure"/> callback selects the database provider and connection for
        /// that storage instance.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This method only makes a database available under <paramref name="storageInstanceId"/>. It does not
        /// register any IdentityServer stores and does not route any IdentityServer data to the
        /// instance. Call <c>AddConfigurationStorage(storageInstanceId)</c> and/or
        /// <c>AddOperationalStorage(storageInstanceId)</c> to store IdentityServer data in it.
        /// </para>
        /// <para>
        /// Call exactly one provider registration method in <paramref name="configure"/>, such as
        /// <c>AddSqlite()</c>, <c>AddPostgreSql()</c>, <c>AddMsSql()</c>, or
        /// <c>AddOracle()</c>. Selecting a second provider for the same instance, in this call or a
        /// later one, throws an <see cref="InvalidOperationException"/>.
        /// </para>
        /// <para>
        /// Each instance is a separate database and is migrated separately.
        /// </para>
        /// </remarks>
        /// <param name="storageInstanceId">The storage instance to register the database for.</param>
        /// <param name="configure">A callback that selects the database provider for <paramref name="storageInstanceId"/>.</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown when a database provider has already been selected for <paramref name="storageInstanceId"/>.
        /// </exception>
        public IIdentityServerBuilder AddStorage(StorageInstanceId storageInstanceId, Action<IStorageBuilder> configure)
        {
            ArgumentNullException.ThrowIfNull(configure);

            builder.Services.AddStorageInternal(storageInstanceId, configure);

            return builder;
        }

        /// <summary>
        /// Registers the default Duende.Storage database (<see cref="StorageInstanceId.Default"/>). The
        /// <paramref name="configure"/> callback selects the database provider and connection.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This method only makes the default database available. It does not register any
        /// IdentityServer stores. Call <c>AddConfigurationStorage()</c> and
        /// <c>AddOperationalStorage()</c> to store IdentityServer data in it, for example:
        /// <code>
        /// builder.AddStorage(s =&gt; s.AddPostgreSql())
        ///        .AddConfigurationStorage()
        ///        .AddOperationalStorage();
        /// </code>
        /// </para>
        /// <para>
        /// Call exactly one provider registration method in <paramref name="configure"/>, such as
        /// <c>AddSqlite()</c>, <c>AddPostgreSql()</c>, <c>AddMsSql()</c>, or
        /// <c>AddOracle()</c>. Selecting a second provider for the default instance, in this call
        /// or a later one (including one made by another product such as User Management), throws an
        /// <see cref="InvalidOperationException"/>.
        /// </para>
        /// <para>
        /// Any data category that is not mapped to a named instance is stored in the default database.
        /// </para>
        /// </remarks>
        /// <param name="configure">A callback that selects the database provider for the default instance.</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown when a database provider has already been selected for the default instance.
        /// </exception>
        public IIdentityServerBuilder AddStorage(Action<IStorageBuilder> configure) => builder.AddStorage(StorageInstanceId.Default, configure);

        /// <summary>
        /// Replaces the in-memory data extension schema store with a mutable one stored in the default
        /// database (<see cref="StorageInstanceId.Default"/>), and registers the <c>ISchemaAdmin</c> API so
        /// schemas can be created and changed at runtime.
        /// </summary>
        /// <remarks>
        /// Equivalent to <c>AddDynamicSchemas(StorageInstanceId.Default)</c>. See that overload for details.
        /// </remarks>
        /// <exception cref="InvalidOperationException">
        /// Thrown when dynamic schemas are already mapped to a storage instance.
        /// </exception>
        public IIdentityServerBuilder AddDynamicSchemas() => builder.AddDynamicSchemas(StorageInstanceId.Default);

        /// <summary>
        /// Replaces the in-memory data extension schema store with a mutable one stored in the given
        /// <paramref name="storageInstanceId"/>, and registers the <c>ISchemaAdmin</c> API so schemas can be
        /// created and changed at runtime.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Register a database for <paramref name="storageInstanceId"/> with <c>AddStorage</c>; otherwise reading
        /// or writing schemas throws at runtime because the instance has no database.
        /// </para>
        /// <para>
        /// Replaces the in-memory schemas: built-in schemas and schemas registered with <c>AddInMemoryDataExtensionSchemas</c>
        /// are not used, and the built-in identity provider schemas (<c>idp:oidc</c>, <c>idp:saml</c>) are not seeded into the
        /// database. When starting from an empty database, create them through <c>ISchemaAdmin</c> if extended properties on
        /// OIDC or SAML providers must be validated against them. <c>ISchemaAdmin</c> is only available with this method.
        /// </para>
        /// <para>
        /// Dynamic schemas can be mapped to only one storage instance per application.
        /// </para>
        /// </remarks>
        /// <param name="storageInstanceId">The storage instance that stores the data extension schemas.</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown when dynamic schemas are already mapped to a storage instance.
        /// </exception>
        public IIdentityServerBuilder AddDynamicSchemas(StorageInstanceId storageInstanceId)
        {
            builder.Services.AddDynamicSchemaStorage(storageInstanceId);
            return builder;
        }


        /// <summary>
        /// Stores IdentityServer operational data in the default database
        /// (<see cref="StorageInstanceId.Default"/>).
        /// </summary>
        /// <remarks>
        /// Equivalent to <c>AddOperationalStorage(StorageInstanceId.Default)</c>. See that overload for details.
        /// </remarks>
        /// <exception cref="InvalidOperationException">
        /// Thrown when operational data is already mapped to a storage instance.
        /// </exception>
        public IIdentityServerBuilder AddOperationalStorage() => builder.AddOperationalStorage(StorageInstanceId.Default);


        /// <summary>
        /// Stores IdentityServer configuration data in the default database
        /// (<see cref="StorageInstanceId.Default"/>).
        /// </summary>
        /// <remarks>
        /// Equivalent to <c>AddConfigurationStorage(StorageInstanceId.Default)</c>. See that overload for details.
        /// </remarks>
        /// <exception cref="InvalidOperationException">
        /// Thrown when configuration data is already mapped to a storage instance.
        /// </exception>
        public IIdentityServerBuilder AddConfigurationStorage() => builder.AddConfigurationStorage(StorageInstanceId.Default);

        /// <summary>
        /// Stores IdentityServer operational data in the given <paramref name="storageInstanceId"/>, and registers
        /// the Duende.Storage-backed operational stores and background services.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Operational data covers persisted grants, device flow codes, pushed authorization requests,
        /// server-side sessions, signing keys, SAML signin state and SAML logout sessions. The server-side
        /// session store is only used when server-side sessions are enabled.
        /// </para>
        /// <para>
        /// Also registers the outbox processor and the background purge of expired data. Both run against
        /// every registered storage instance, not only <paramref name="storageInstanceId"/>.
        /// </para>
        /// <para>
        /// Register a database for <paramref name="storageInstanceId"/> with <c>AddStorage</c>; otherwise the first
        /// operational read or write throws at runtime because the instance has no database.
        /// </para>
        /// <para>
        /// Stores that replace IdentityServer's defaults keep their registration: a custom store registered
        /// before this call is left in place, and a store selector called after it (for example
        /// <c>AddInMemoryPersistedGrants()</c>) replaces the Duende.Storage-backed store.
        /// </para>
        /// </remarks>
        /// <param name="storageInstanceId">The storage instance that stores operational data.</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown when operational data is already mapped to a storage instance.
        /// </exception>
        public IIdentityServerBuilder AddOperationalStorage(StorageInstanceId storageInstanceId)
        {
            var services = builder.Services;

            services.GetOrAddStorageInstanceRouter().AddMapping(DataCategoryName.Operational, storageInstanceId);

            // Persisted grant store
            services.TryAddTransientOrDefault<IPersistedGrantStore, InMemoryPersistedGrantStore, PersistedGrantStore>();
            services.TryAddTransient<PersistedGrantRepository>();
            services.AddDsoRegistration<PersistedGrantDso.V1>();

            // signing store
            services.TryAddTransientOrDefault<ISigningKeyStore, FileSystemKeyStore, SigningKeyStore>();
            services.TryAddTransient<KeyRepository>();
            services.AddDsoRegistration<KeyDso.V1>();

            services.AddDsoRegistration<PushedAuthorizationDso.V1>();
            services.TryAddTransientOrDefault<IPushedAuthorizationRequestStore, InMemoryPushedAuthorizationRequestStore,
                    PushedAuthorizationStore>();
            services.TryAddTransient<PushedAuthorizationRepository>();

            // Server-side session store only resolves when IServerSideSessionsMarker is registered
            services.AddDsoRegistration<ServerSideSessionDso.V1>();
            services.TryAddSingleton<IStorageBackedSessionsMarker, StorageBackedSessionsMarker>();
            builder.AddServerSideSessionStore<ServerSideSessionStore>();
            services.TryAddTransient<ServerSideSessionRepository>();


            services.AddDsoRegistration<SamlSigninStateDso.V1>();
            services.AddDsoRegistration<SamlLogoutSessionDso.V1>();
            services.TryAddTransientOrDefault<ISamlSigninStateStore, InMemorySamlSigninStateStore,
                SamlSigninStateStore>();
            services.TryAddSingleton<ISamlSigninStateSerializer, JsonSamlSigninStateSerializer>();
            services.TryAddTransientOrDefault<ISamlLogoutSessionStore, InMemorySamlLogoutSessionStore,
                SamlLogoutSessionStore>();
            services.TryAddTransient<SamlSigninStateRepository>();
            services.TryAddTransient<SamlLogoutSessionRepository>();


            services.TryAddTransient<DeviceFlowRepository>();
            services.AddDsoRegistration<DeviceFlowDso.V1>();
            services.TryAddTransientOrDefault<IDeviceFlowStore, InMemoryDeviceFlowStore, DeviceFlowStore>();


            // Session expiration handler (back-channel logout for storage users). TryAddSingleton
            // keeps repeated AddStorage() calls idempotent for the marker and subscription; the
            // keyed handler registration uses TryAddKeyedTransient for the same reason. The handler
            // is registered directly rather than wrapped in a pool-establishing decorator: ambient
            // context for an outbox event is established once, around the handler call, by whichever
            // IAmbientOutboxProcessingContextProvider is registered. IdentityServer registers none,
            // so Storage's no-op default applies and every event processes in its only pool,
            // PoolId.Default. Duende.Spaces supplies a space-aware provider when spaces are in play.
            services.TryAddSingleton<IOutboxSubscription, SessionExpirationSubscription>();
            services.TryAddKeyedTransient<IOutboxSubscriptionHandler, SessionExpirationHandler>(
                SessionExpirationSubscription.Name.Value);

            // Outbox processor and background purge hosted services. TryAddEnumerable keeps repeated
            // AddStorage() calls from registering duplicate IHostedService instances.
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, OutboxProcessorHost>());
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, Hosting.StoragePurgeHost>());

            return builder;
        }

        /// <summary>
        /// Stores IdentityServer configuration data in the given <paramref name="storageInstanceId"/>, and registers
        /// the Duende.Storage-backed configuration stores and admin APIs.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Configuration data covers clients, identity providers, SAML service providers, API resources,
        /// API scopes and identity resources. The CORS policy service reads allowed origins from the stored
        /// clients. The admin APIs (<c>IClientAdmin</c>, <c>IIdentityProviderAdmin</c>,
        /// <c>ISamlServiceProviderAdmin</c>, <c>IApiResourceAdmin</c>, <c>IApiScopeAdmin</c>,
        /// <c>IIdentityResourceAdmin</c>) manage this data.
        /// </para>
        /// <para>
        /// Register a database for <paramref name="storageInstanceId"/> with <c>AddStorage</c>; otherwise the first
        /// configuration read or write throws at runtime because the instance has no database.
        /// </para>
        /// <para>
        /// Data extension schemas are served by the in-memory schema store that <c>AddStorage</c>
        /// registers. The built-in identity provider schemas (<c>idp:oidc</c>, <c>idp:saml</c>) come
        /// from the OIDC and SAML dynamic provider features (<c>AddOidcDynamicProvider</c>,
        /// <c>AddSamlDynamicProvider</c>), not from this method. Use
        /// <c>AddInMemoryDataExtensionSchemas(schemas)</c> to add or replace schemas, or
        /// <c>AddDynamicSchemas()</c> for a mutable schema store in the database.
        /// </para>
        /// <para>
        /// Stores that replace IdentityServer's defaults keep their registration: a custom store registered
        /// before this call is left in place, and a store selector called after it (for example
        /// <c>AddInMemoryClients()</c>) replaces the Duende.Storage-backed store and disables the matching
        /// admin API.
        /// </para>
        /// </remarks>
        /// <param name="storageInstanceId">The storage instance that stores configuration data.</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown when configuration data is already mapped to a storage instance.
        /// </exception>
        public IIdentityServerBuilder AddConfigurationStorage(StorageInstanceId storageInstanceId)
        {
            var services = builder.Services;

            services.GetOrAddStorageInstanceRouter().AddMapping(DataCategoryName.Configuration, storageInstanceId);

            // Clients
            services.TryAddTransient<IClientAdmin, ClientAdmin>();
            services.AddDsoRegistration<ClientDso.V1>();
            services.TryAddTransient<ClientRepository>();
            services.TryAddTransientOrDefault<IClientStore, EmptyClientStore, ClientStore>();
            // CorsPolicy reads from ClientStore
            services.TryAddTransientOrDefault<ICorsPolicyService, DefaultCorsPolicyService, StorageCorsPolicyService>();

            // Api scopes
            services.TryAddTransient<IIdentityProviderAdmin, IdentityProviderAdmin>();
            services.AddDsoRegistration<IdentityProviderDso.V1>();
            services.TryAddTransient<IdentityProviderRepository>();
            services.TryAddTransientOrDefault<IIdentityProviderStore, NopIdentityProviderStore, IdentityProviderStore>();

            // Saml
            services.TryAddTransient<ISamlServiceProviderAdmin, SamlServiceProviderAdmin>();
            services.AddDsoRegistration<SamlServiceProviderDso.V1>();
            services.TryAddTransient<SamlServiceProviderRepository>();
            services.TryAddTransientOrDefault<ISamlServiceProviderStore, EmptySamlServiceProviderStore, SamlServiceProviderStore>();

            // Resources (ApiScope, ApiResource, IdentityResource)
            services.TryAddTransient<IApiResourceAdmin, ApiResourceAdmin>();
            services.TryAddTransient<IApiScopeAdmin, ApiScopeAdmin>();
            services.TryAddTransient<IIdentityResourceAdmin, IdentityResourceAdmin>();
            services.AddDsoRegistration<ApiResourceDso.V1>();
            services.AddDsoRegistration<ApiScopeDso.V1>();
            services.AddDsoRegistration<IdentityResourceDso.V1>();
            services.TryAddTransient<ApiResourceRepository>();
            services.TryAddTransient<ApiScopeRepository>();
            services.TryAddTransient<IdentityResourceRepository>();
            services.TryAddTransientOrDefault<IResourceStore, EmptyResourceStore, StorageResourceStore>();

            return builder;
        }
    }
}


