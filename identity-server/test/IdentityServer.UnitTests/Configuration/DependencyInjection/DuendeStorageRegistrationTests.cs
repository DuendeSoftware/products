// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer;
using Duende.IdentityServer.Admin;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Hosting.DynamicProviders;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Saml;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Stores;
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
using Duende.Storage.EntityAttributeValue;
using Duende.Storage.EntityAttributeValue.Internal;
using Duende.Storage.Internal;
using Duende.Storage.Schema;
using Duende.Storage.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace UnitTests.Configuration.DependencyInjection;

public class DuendeStorageRegistrationTests
{
    private static (IServiceCollection Services, IIdentityServerBuilder Builder) CreateBuilder()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var dbName = $"duende_storage_registration_{Guid.NewGuid():N}";
        services.AddStorageInternal(storage =>
            storage.AddSqliteStore(opt => opt.ConnectionString = $"Data Source={dbName};Mode=Memory;Cache=Shared"));

        // AddIdentityServer() registers the full set of platform/core/validator services (client
        // configuration validator, identity provider configuration validator, event service, etc.)
        // that store decorators depend on, matching how AddStorage() is used by a real host.
        // It also registers default in-memory persisted grant/PAR stores, which AddStorage()
        // (called afterwards in these tests) intentionally replaces with storage-backed ones.
        var builder = services.AddIdentityServer();
        return (services, builder);
    }

    private static ServiceProvider BuildAndMigrate(IServiceCollection services)
    {
        var sp = services.BuildServiceProvider();
        var pooledStore = sp.GetRequiredService<IPooledStore>();
        ((IDatabaseSchema)pooledStore).MigrateAsync(CancellationToken.None).GetAwaiter().GetResult();
        return sp;
    }

    private static void AssertSingleDescriptor<TService>(IServiceCollection services, Type expectedImplementationType) where TService : notnull
    {
        var serviceProvider = services.BuildServiceProvider();
        var expectedService = serviceProvider.GetRequiredService<TService>();
        expectedService.ShouldBeOfType(expectedImplementationType);
    }

    [Fact]
    public void AddStorage_resolves_all_storage_backed_stores_with_intended_decorators()
    {
        var (services, builder) = CreateBuilder();
        builder.AddStorage(_ => { });
        using var sp = BuildAndMigrate(services);

        sp.GetRequiredService<IClientStore>().ShouldBeOfType<ClientStore>();
        sp.GetRequiredService<IIdentityProviderStore>().ShouldBeOfType<IdentityProviderStore>();
        sp.GetRequiredService<ISamlServiceProviderStore>().ShouldBeOfType<SamlServiceProviderStore>();
        sp.GetRequiredService<IResourceStore>().ShouldBeOfType<StorageResourceStore>();
        sp.GetRequiredService<ICorsPolicyService>().ShouldBeOfType<StorageCorsPolicyService>();
        sp.GetRequiredService<IPersistedGrantStore>().ShouldBeOfType<PersistedGrantStore>();
        sp.GetRequiredService<IDeviceFlowStore>().ShouldBeOfType<DeviceFlowStore>();
        sp.GetRequiredService<IPushedAuthorizationRequestStore>().ShouldBeOfType<PushedAuthorizationStore>();
        sp.GetRequiredService<ISigningKeyStore>().ShouldBeOfType<SigningKeyStore>();
        sp.GetRequiredService<ISamlSigninStateStore>().ShouldBeOfType<SamlSigninStateStore>();
        sp.GetRequiredService<ISamlLogoutSessionStore>().ShouldBeOfType<SamlLogoutSessionStore>();
    }

    [Fact]
    public void AddStorage_resolves_all_six_admin_interfaces()
    {
        var (services, builder) = CreateBuilder();
        builder.AddStorage(_ => { });
        using var sp = BuildAndMigrate(services);
        using var scope = sp.CreateScope();

        scope.ServiceProvider.GetRequiredService<IClientAdmin>().ShouldBeOfType<ClientAdmin>();
        scope.ServiceProvider.GetRequiredService<IIdentityProviderAdmin>().ShouldBeOfType<IdentityProviderAdmin>();
        scope.ServiceProvider.GetRequiredService<ISamlServiceProviderAdmin>().ShouldBeOfType<SamlServiceProviderAdmin>();
        scope.ServiceProvider.GetRequiredService<IApiResourceAdmin>().ShouldBeOfType<ApiResourceAdmin>();
        scope.ServiceProvider.GetRequiredService<IApiScopeAdmin>().ShouldBeOfType<ApiScopeAdmin>();
        scope.ServiceProvider.GetRequiredService<IIdentityResourceAdmin>().ShouldBeOfType<IdentityResourceAdmin>();
    }

    [Fact]
    public void AddStorage_default_schema_store_is_in_memory_seeded_with_built_ins_and_has_no_admin()
    {
        var (services, builder) = CreateBuilder();
        builder.AddStorage(_ => { });
        using var sp = BuildAndMigrate(services);

        var schemaStore = sp.GetRequiredService<ISchemaStore>();
        schemaStore.ShouldBeOfType<InMemorySchemaStore>();

        var oidcSchema = schemaStore.GetAsync(SchemaId.IdentityProvider("oidc"), CancellationToken.None)
            .GetAwaiter().GetResult();
        oidcSchema.ShouldNotBe(AttributeSchema.Empty);

        var samlSchema = schemaStore.GetAsync(SchemaId.IdentityProvider("saml"), CancellationToken.None)
            .GetAwaiter().GetResult();
        samlSchema.ShouldNotBe(AttributeSchema.Empty);

        sp.GetService<ISchemaAdmin>().ShouldBeNull();
    }

    [Fact]
    public void AddStorage_without_server_side_sessions_marker_resolves_no_session_store()
    {
        var (services, builder) = CreateBuilder();
        builder.AddStorage(_ => { });
        using var sp = BuildAndMigrate(services);

        // No IServerSideSessionsMarker registered (AddServerSideSessions() was never called), so the
        // conditional factory in AddServerSideSessionStore<T>() intentionally resolves null.
        sp.GetService<IServerSideSessionStore>().ShouldBeNull();
    }

    [Fact]
    public void AddStorage_with_server_side_sessions_marker_resolves_storage_backed_session_store()
    {
        var (services, builder) = CreateBuilder();
        builder.AddServerSideSessions();
        builder.AddStorage(_ => { });
        using var sp = BuildAndMigrate(services);

        sp.GetRequiredService<IServerSideSessionStore>().ShouldBeOfType<ServerSideSessionStore>();
    }

    [Fact]
    public void AddStorage_called_twice_leaves_one_hosted_service_per_type()
    {
        var (services, builder) = CreateBuilder();

        builder.AddStorage(_ => { });
        builder.AddStorage(_ => { });

        var hostedServiceImplementationTypes = services
            .Where(x => x.ServiceType == typeof(Microsoft.Extensions.Hosting.IHostedService))
            .Select(x => x.ImplementationType)
            .ToList();

        hostedServiceImplementationTypes.Count.ShouldBe(hostedServiceImplementationTypes.Distinct().Count());
    }

    [Fact]
    public void AddInMemoryClients_after_AddStorage_disables_the_client_admin()
    {
        var (services, builder) = CreateBuilder();
        builder.AddStorage(_ => { });
        builder.AddInMemoryClients(Array.Empty<Client>());
        using var sp = BuildAndMigrate(services);

        Should.Throw<NotSupportedException>(sp.GetService<IClientAdmin>);
    }

    [Fact]
    public void AddInMemoryIdentityResources_after_AddStorage_disables_all_resource_admins()
    {
        var (services, builder) = CreateBuilder();
        builder.AddStorage(_ => { });
        builder.AddInMemoryIdentityResources(Array.Empty<IdentityResource>());
        using var sp = BuildAndMigrate(services);

        Should.Throw<NotSupportedException>(sp.GetService<IIdentityResourceAdmin>);
        Should.Throw<NotSupportedException>(sp.GetService<IApiResourceAdmin>);
        Should.Throw<NotSupportedException>(sp.GetService<IApiScopeAdmin>);
    }

    [Fact]
    public void AddInMemoryApiResources_after_AddStorage_disables_all_resource_admins()
    {
        var (services, builder) = CreateBuilder();
        builder.AddStorage(_ => { });
        builder.AddInMemoryApiResources(Array.Empty<ApiResource>());
        using var sp = BuildAndMigrate(services);

        Should.Throw<NotSupportedException>(sp.GetService<IIdentityResourceAdmin>);
        Should.Throw<NotSupportedException>(sp.GetService<IApiResourceAdmin>);
        Should.Throw<NotSupportedException>(sp.GetService<IApiScopeAdmin>);
    }

    [Fact]
    public void AddInMemoryApiScopes_after_AddStorage_disables_all_resource_admins()
    {
        var (services, builder) = CreateBuilder();
        builder.AddStorage(_ => { });
        builder.AddInMemoryApiScopes(Array.Empty<ApiScope>());
        using var sp = BuildAndMigrate(services);

        Should.Throw<NotSupportedException>(sp.GetService<IIdentityResourceAdmin>);
        Should.Throw<NotSupportedException>(sp.GetService<IApiResourceAdmin>);
        Should.Throw<NotSupportedException>(sp.GetService<IApiScopeAdmin>);
    }

    [Fact]
    public void AddInMemoryIdentityProviders_after_AddStorage_disables_the_identity_provider_admin()
    {
        var (services, builder) = CreateBuilder();
        builder.AddStorage(_ => { });
        builder.AddInMemoryIdentityProviders(Array.Empty<IdentityProvider>());
        using var sp = BuildAndMigrate(services);

        Should.Throw<NotSupportedException>(sp.GetService<IIdentityProviderAdmin>);
    }

    [Fact]
    public void AddInMemorySamlServiceProviders_after_AddStorage_disables_the_saml_service_provider_admin()
    {
        var (services, builder) = CreateBuilder();
        builder.AddStorage(_ => { });
        builder.AddInMemorySamlServiceProviders(Array.Empty<SamlServiceProvider>());
        using var sp = BuildAndMigrate(services);

        Should.Throw<NotSupportedException>(sp.GetService<ISamlServiceProviderAdmin>);
    }

    [Fact]
    public void AddInMemoryClients_after_AddStorage_replaces_client_store_and_storage_cors_policy()
    {
        var (services, builder) = CreateBuilder();
        builder.AddStorage(_ => { });
        builder.AddInMemoryClients(Array.Empty<Client>());
        using var sp = BuildAndMigrate(services);

        AssertSingleDescriptor<IClientStore>(services, typeof(ValidatingClientStore<InMemoryClientStore>));
        AssertSingleDescriptor<ICorsPolicyService>(services, typeof(InMemoryCorsPolicyService));

        sp.GetRequiredService<IClientStore>().ShouldBeOfType<ValidatingClientStore<InMemoryClientStore>>();
        sp.GetRequiredService<ICorsPolicyService>().ShouldBeOfType<InMemoryCorsPolicyService>();
    }

    [Fact]
    public void AddInMemoryClients_does_not_replace_a_custom_cors_policy_service()
    {
        var services = new ServiceCollection();
        var identityServerBuilder = new IdentityServerBuilder(services);
        services.AddTransient<ICorsPolicyService, CustomCorsPolicyService>();

        identityServerBuilder.AddInMemoryClients(Array.Empty<Client>());

        AssertSingleDescriptor<ICorsPolicyService>(services, typeof(CustomCorsPolicyService));
    }

    [Fact]
    public void AddInMemoryClients_does_not_replace_a_custom_cors_policy_service_registered_after_AddStorage()
    {
        var (services, builder) = CreateBuilder();
        builder.AddStorage(_ => { });
        builder.AddCorsPolicyService<CustomCorsPolicyService>();

        builder.AddInMemoryClients(Array.Empty<Client>());

        AssertSingleDescriptor<ICorsPolicyService>(services, typeof(CustomCorsPolicyService));
    }

    [Fact]
    public void AddInMemoryIdentityResources_after_AddStorage_replaces_the_whole_resource_store()
    {
        var (services, builder) = CreateBuilder();
        builder.AddStorage(_ => { });
        builder.AddInMemoryIdentityResources([new IdentityResource("openid", ["sub"])]);
        using var sp = BuildAndMigrate(services);

        AssertSingleDescriptor<IResourceStore>(services, typeof(InMemoryResourcesStore));
        sp.GetRequiredService<IResourceStore>().ShouldBeOfType<InMemoryResourcesStore>();
    }

    [Fact]
    public void AddInMemoryApiResources_after_AddStorage_replaces_the_whole_resource_store()
    {
        var (services, builder) = CreateBuilder();
        builder.AddStorage(_ => { });
        builder.AddInMemoryApiResources([new ApiResource("api1")]);
        using var sp = BuildAndMigrate(services);

        AssertSingleDescriptor<IResourceStore>(services, typeof(InMemoryResourcesStore));
        sp.GetRequiredService<IResourceStore>().ShouldBeOfType<InMemoryResourcesStore>();
    }

    [Fact]
    public void AddInMemoryApiScopes_after_AddStorage_replaces_the_whole_resource_store()
    {
        var (services, builder) = CreateBuilder();
        builder.AddStorage(_ => { });
        builder.AddInMemoryApiScopes([new ApiScope("scope1")]);
        using var sp = BuildAndMigrate(services);

        AssertSingleDescriptor<IResourceStore>(services, typeof(InMemoryResourcesStore));
        sp.GetRequiredService<IResourceStore>().ShouldBeOfType<InMemoryResourcesStore>();
    }

    [Fact]
    public void AddInMemoryApiResources_alone_does_not_provide_identity_resources_or_api_scopes()
    {
        var (services, builder) = CreateBuilder();
        builder.AddStorage(_ => { });
        builder.AddInMemoryApiResources([new ApiResource("api1")]);
        using var sp = BuildAndMigrate(services);

        var store = sp.GetRequiredService<IResourceStore>();
        var resources = store.GetAllResourcesAsync(CancellationToken.None).GetAwaiter().GetResult();

        resources.ApiResources.Count.ShouldBe(1);
        resources.IdentityResources.ShouldBeEmpty();
        resources.ApiScopes.ShouldBeEmpty();
    }

    [Fact]
    public void AddInMemoryIdentityProviders_after_AddStorage_replaces_the_identity_provider_store()
    {
        var (services, builder) = CreateBuilder();
        builder.AddStorage(_ => { });
        builder.AddInMemoryIdentityProviders(Array.Empty<IdentityProvider>());
        using var sp = BuildAndMigrate(services);

        AssertSingleDescriptor<IIdentityProviderStore>(
            services,
            typeof(NonCachingIdentityProviderStore<ValidatingIdentityProviderStore<InMemoryIdentityProviderStore>>));
        sp.GetRequiredService<IIdentityProviderStore>()
            .ShouldBeOfType<NonCachingIdentityProviderStore<ValidatingIdentityProviderStore<InMemoryIdentityProviderStore>>>();
    }

    [Fact]
    public void AddInMemoryOidcProviders_after_AddStorage_replaces_the_identity_provider_store_through_the_shared_path()
    {
        var (services, builder) = CreateBuilder();
        builder.AddStorage(_ => { });
        builder.AddInMemoryOidcProviders(Array.Empty<OidcProvider>());
        using var sp = BuildAndMigrate(services);

        AssertSingleDescriptor<IIdentityProviderStore>(
            services,
            typeof(NonCachingIdentityProviderStore<ValidatingIdentityProviderStore<InMemoryIdentityProviderStore>>));
        sp.GetRequiredService<IIdentityProviderStore>()
            .ShouldBeOfType<NonCachingIdentityProviderStore<ValidatingIdentityProviderStore<InMemoryIdentityProviderStore>>>();
    }

    [Fact]
    public void AddInMemorySamlProviders_after_AddStorage_replaces_the_identity_provider_store_through_the_shared_path()
    {
        var (services, builder) = CreateBuilder();
        builder.AddStorage(_ => { });
        builder.AddInMemorySamlProviders(Array.Empty<SamlProvider>());
        using var sp = BuildAndMigrate(services);

        AssertSingleDescriptor<IIdentityProviderStore>(
            services,
            typeof(NonCachingIdentityProviderStore<ValidatingIdentityProviderStore<InMemoryIdentityProviderStore>>));
        sp.GetRequiredService<IIdentityProviderStore>()
            .ShouldBeOfType<NonCachingIdentityProviderStore<ValidatingIdentityProviderStore<InMemoryIdentityProviderStore>>>();
    }

    [Fact]
    public void AddInMemorySamlServiceProviders_after_AddStorage_replaces_the_saml_service_provider_store()
    {
        var (services, builder) = CreateBuilder();
        builder.AddStorage(_ => { });
        builder.AddInMemorySamlServiceProviders(Array.Empty<SamlServiceProvider>());
        using var sp = BuildAndMigrate(services);

        AssertSingleDescriptor<ISamlServiceProviderStore>(
            services,
            typeof(ValidatingSamlServiceProviderStore<InMemorySamlServiceProviderStore>));
        sp.GetRequiredService<ISamlServiceProviderStore>()
            .ShouldBeOfType<ValidatingSamlServiceProviderStore<InMemorySamlServiceProviderStore>>();
    }

    [Fact]
    public void AddInMemoryPersistedGrants_after_AddStorage_replaces_persisted_grant_and_device_flow_stores()
    {
        var (services, builder) = CreateBuilder();
        builder.AddStorage(_ => { });
        builder.AddInMemoryPersistedGrants();
        using var sp = BuildAndMigrate(services);

        AssertSingleDescriptor<IPersistedGrantStore>(services, typeof(InMemoryPersistedGrantStore));
        AssertSingleDescriptor<IDeviceFlowStore>(services, typeof(InMemoryDeviceFlowStore));
        sp.GetRequiredService<IPersistedGrantStore>().ShouldBeOfType<InMemoryPersistedGrantStore>();
        sp.GetRequiredService<IDeviceFlowStore>().ShouldBeOfType<InMemoryDeviceFlowStore>();
    }

    [Fact]
    public void AddInMemoryPushedAuthorizationRequests_after_AddStorage_replaces_the_par_store()
    {
        var (services, builder) = CreateBuilder();
        builder.AddStorage(_ => { });
        builder.AddInMemoryPushedAuthorizationRequests();
        using var sp = BuildAndMigrate(services);

        AssertSingleDescriptor<IPushedAuthorizationRequestStore>(services, typeof(InMemoryPushedAuthorizationRequestStore));
        sp.GetRequiredService<IPushedAuthorizationRequestStore>().ShouldBeOfType<InMemoryPushedAuthorizationRequestStore>();
    }

    [Fact]
    public void AddInMemoryDataExtensionSchemas_after_AddStorage_replaces_the_schema_store_and_makes_admin_unresolvable()
    {
        var (services, builder) = CreateBuilder();
        builder.AddStorage(_ => { });

        var customSchemaId = SchemaId.Create("custom:widget");
        builder.AddInMemoryDataExtensionSchemas([
            new SchemaConfiguration { SchemaId = customSchemaId, DisplayName = "Widget" }
        ]);
        using var sp = BuildAndMigrate(services);

        AssertSingleDescriptor<ISchemaStore>(services, typeof(InMemorySchemaStore));

        // In-memory schemas are immutable at runtime, so ISchemaAdmin is intentionally unsupported.
        // Resolving it must throw a clear NotSupportedException rather than silently returning null,
        // so callers using ISchemaAdmin against in-memory schemas fail loudly at the point of use.
        Should.Throw<NotSupportedException>(sp.GetService<ISchemaAdmin>);

        var schemaStore = sp.GetRequiredService<ISchemaStore>();

        // Built-ins remain available alongside the caller-supplied schema.
        schemaStore.GetAsync(SchemaId.IdentityProvider("oidc"), CancellationToken.None)
            .GetAwaiter().GetResult().ShouldNotBe(AttributeSchema.Empty);

        schemaStore.GetAsync(customSchemaId, CancellationToken.None)
            .GetAwaiter().GetResult().ShouldNotBe(AttributeSchema.Empty);
    }

    [Fact]
    public void AddPersistedGrantStore_after_AddStorage_replaces_the_storage_backed_implementation()
    {
        var (services, builder) = CreateBuilder();
        builder.AddStorage(_ => { });
        builder.AddPersistedGrantStore<CustomPersistedGrantStore>();

        AssertSingleDescriptor<IPersistedGrantStore>(services, typeof(CustomPersistedGrantStore));
    }

    [Fact]
    public void AddSigningKeyStore_after_AddStorage_replaces_the_storage_backed_implementation()
    {
        var (services, builder) = CreateBuilder();
        builder.AddStorage(_ => { });
        builder.AddSigningKeyStore<CustomSigningKeyStore>();

        AssertSingleDescriptor<ISigningKeyStore>(services, typeof(CustomSigningKeyStore));
    }

    [Fact]
    public void AddSamlSigninStateStore_after_AddStorage_replaces_the_storage_backed_implementation()
    {
        var (services, builder) = CreateBuilder();
        builder.AddStorage(_ => { });
        builder.AddSamlSigninStateStore<CustomSamlSigninStateStore>();

        AssertSingleDescriptor<ISamlSigninStateStore>(services, typeof(CustomSamlSigninStateStore));
    }

    [Fact]
    public void AddSamlLogoutSessionStore_after_AddStorage_replaces_the_storage_backed_implementation()
    {
        var (services, builder) = CreateBuilder();
        builder.AddStorage(_ => { });
        builder.AddSamlLogoutSessionStore<CustomSamlLogoutSessionStore>();

        AssertSingleDescriptor<ISamlLogoutSessionStore>(services, typeof(CustomSamlLogoutSessionStore));
    }

    [Fact]
    public void AddCorsPolicyService_after_AddStorage_replaces_the_storage_backed_implementation()
    {
        var (services, builder) = CreateBuilder();
        builder.AddStorage(_ => { });
        builder.AddCorsPolicyService<CustomCorsPolicyService>();

        AssertSingleDescriptor<ICorsPolicyService>(services, typeof(CustomCorsPolicyService));
    }

    [Fact]
    public void AddStorage_after_AddSaml_replaces_default_signin_state_store()
    {
        var (services, builder) = CreateBuilder();

        builder.AddSaml();
        builder.AddStorage(_ => { });

        AssertSingleDescriptor<ISamlSigninStateStore>(
            services,
            typeof(SamlSigninStateStore));
    }

    private sealed class CustomCorsPolicyService : ICorsPolicyService
    {
        public Task<bool> IsOriginAllowedAsync(string origin, Ct ct) => Task.FromResult(true);
    }

    private sealed class CustomPersistedGrantStore : IPersistedGrantStore
    {
        public Task StoreAsync(PersistedGrant grant, Ct ct) => Task.CompletedTask;
        public Task<PersistedGrant?> GetAsync(string key, Ct ct) => Task.FromResult<PersistedGrant?>(null);
        public Task<IReadOnlyCollection<PersistedGrant>> GetAllAsync(PersistedGrantFilter filter, Ct ct) =>
            Task.FromResult<IReadOnlyCollection<PersistedGrant>>([]);
        public Task RemoveAsync(string key, Ct ct) => Task.CompletedTask;
        public Task RemoveAllAsync(PersistedGrantFilter filter, Ct ct) => Task.CompletedTask;
    }

    private sealed class CustomSigningKeyStore : ISigningKeyStore
    {
        public Task<IReadOnlyCollection<SerializedKey>> LoadKeysAsync(Ct ct) => Task.FromResult<IReadOnlyCollection<SerializedKey>>([]);
        public Task StoreKeyAsync(SerializedKey key, Ct ct) => Task.CompletedTask;
        public Task DeleteKeyAsync(string id, Ct ct) => Task.CompletedTask;
    }

    private sealed class CustomSamlSigninStateStore : ISamlSigninStateStore
    {
        public Task<Guid> StoreSigninRequestStateAsync(SamlAuthenticationState state, Ct ct = default) => Task.FromResult(Guid.NewGuid());
        public Task<SamlAuthenticationState?> RetrieveSigninRequestStateAsync(Guid stateId, Ct ct = default) => Task.FromResult<SamlAuthenticationState?>(null);
        public Task UpdateSigninRequestStateAsync(Guid stateId, SamlAuthenticationState state, Ct ct = default) => Task.CompletedTask;
        public Task RemoveSigninRequestStateAsync(Guid stateId, Ct ct = default) => Task.CompletedTask;
    }

    private sealed class CustomSamlLogoutSessionStore : ISamlLogoutSessionStore
    {
        public Task StoreAsync(SamlLogoutSession session, Ct ct) => Task.CompletedTask;
        public Task<SamlLogoutSession?> GetByLogoutIdAsync(string logoutId, Ct ct) => Task.FromResult<SamlLogoutSession?>(null);
        public Task<bool> TryRecordResponseAsync(string requestId, string issuer, bool success, Ct ct) => Task.FromResult(false);
        public Task RemoveAsync(string logoutId, Ct ct) => Task.CompletedTask;
    }
}
