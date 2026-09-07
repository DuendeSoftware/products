// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Hosting.OutboxProcessor;
using Duende.IdentityServer.Saml;
using Duende.IdentityServer.Stores.Storage;
using Duende.IdentityServer.Stores.Storage.DeviceFlow;
using Duende.IdentityServer.Stores.Storage.PersistedGrants;
using Duende.IdentityServer.Stores.Storage.PushedAuthorization;
using Duende.IdentityServer.Stores.Storage.SamlLogoutSession;
using Duende.IdentityServer.Stores.Storage.SamlSigninState;
using Duende.IdentityServer.Stores.Storage.ServerSideSessions;
using Duende.IdentityServer.Stores.Storage.SigningKeys;
using Duende.Storage.Internal;
using Duende.Storage.Internal.Builder;
using Duende.Storage.Internal.Outbox;
using Duende.Storage.Schema;
using Duende.Storage.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Duende.IdentityServer.IntegrationTests.Common;

/// <summary>
/// Shared helper for configuring IStorage-backed operational stores in
/// protocol-level integration tests. Registers only the operational
/// Duende.Storage-backed stores (persisted grants, device flow, pushed
/// authorization requests, server-side sessions, signing keys, SAML signin
/// state, SAML logout sessions) against an in-memory SQLite database.
/// </summary>
/// <remarks>
/// This intentionally does NOT call <c>AddStorage()</c>, which also
/// registers configuration stores (clients, resources, SAML service
/// providers). <see cref="IdentityServerPipeline"/> seeds configuration data
/// via in-memory stores (<c>AddInMemoryClients()</c>, etc.) earlier in
/// <c>ConfigureServices</c>; this helper runs afterward via
/// <c>OnPostConfigureServices</c>, and per the "later selector wins" override
/// rule, calling the full <c>AddStorage()</c> here would replace those
/// in-memory configuration stores with empty storage-backed ones. Registering
/// only the operational store selectors preserves the seeded configuration
/// data while still exercising storage-backed operational stores.
/// </remarks>
internal static class StorageTestHelper
{
    /// <summary>
    /// Registers Storage-backed operational stores with a unique in-memory
    /// SQLite database. Call this from <see cref="IdentityServerPipeline.OnPostConfigureServices"/>.
    /// Disables the background storage purge host to prevent a background
    /// purge loop from running during tests.
    /// </summary>
    public static void AddOperationalStorageForTesting(this IServiceCollection services)
    {
        var dbName = $"protocol_{Guid.NewGuid():N}";

        services.AddStorageInternal(storage =>
            storage.AddSqliteStore(opt =>
                opt.ConnectionString = $"Data Source={dbName};Mode=Memory;Cache=Shared"));

        var builder = services.AddIdentityServerBuilder();

        // Pool context accessor: required by PoolAwareOutboxHandler (used by the session
        // expiration keyed handler below) to resolve the storage pool for the current scope.
        // Not registered by AddStorageInternal(), and AddStorage()'s private
        // AddStorageInfrastructure() helper isn't reachable from this test-only registration
        // path, so it must be registered explicitly here.
        services.TryAddSingleton<IPoolContextAccessor, PoolContextAccessor>();

        // DSO type registrations (required for deserialization by the storage layer)
        services.AddDsoRegistration<PersistedGrantDso.V1>();
        services.AddDsoRegistration<DeviceFlowDso.V1>();
        services.AddDsoRegistration<KeyDso.V1>();
        services.AddDsoRegistration<PushedAuthorizationDso.V1>();
        services.AddDsoRegistration<SamlSigninStateDso.V1>();
        services.AddDsoRegistration<ServerSideSessionDso.V1>();
        services.AddDsoRegistration<SamlLogoutSessionDso.V1>();

        // Repositories (scoped per-request lifetime matches store handle semantics)
        services.TryAddScoped<PersistedGrantRepository>();
        services.TryAddScoped<DeviceFlowRepository>();
        services.TryAddScoped<KeyRepository>();
        services.TryAddScoped<PushedAuthorizationRepository>();
        services.TryAddScoped<SamlSigninStateRepository>();
        services.TryAddScoped<ServerSideSessionRepository>();
        services.TryAddScoped<SamlLogoutSessionRepository>();

        // Operational stores only — configuration stores (clients, resources, SAML
        // service providers) are intentionally left as whatever was registered earlier
        // in the pipeline (typically in-memory, seeded by test fixtures).
        builder.AddPersistedGrantStore<PersistedGrantStore>();
        builder.AddDeviceFlowStore<DeviceFlowStore>();
        builder.AddPushedAuthorizationRequestStore<PushedAuthorizationStore>();
        builder.AddServerSideSessionStore<ServerSideSessionStore>();
        builder.AddSigningKeyStore<SigningKeyStore>();
        builder.AddSamlSigninStateStore<SamlSigninStateStore>();
        services.TryAddSingleton<ISamlSigninStateSerializer, JsonSamlSigninStateSerializer>();
        builder.AddSamlLogoutSessionStore<SamlLogoutSessionStore>();

        services.TryAddSingleton<IStorageBackedSessionsMarker, StorageBackedSessionsMarker>();
        services.TryAddSingleton<IOutboxSubscriber, SessionExpirationSubscriber>();
        services.TryAddKeyedTransient<IOutboxSubscriberHandler>(SessionExpirationSubscriber.Name.Value,
            (sp, _) => new PoolAwareOutboxHandler(
                ActivatorUtilities.CreateInstance<SessionExpirationHandler>(sp),
                sp.GetRequiredService<IPoolContextAccessor>()));

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, OutboxProcessorHost>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, Duende.IdentityServer.Hosting.StoragePurgeHost>());

        services.Configure<IdentityServerOptions>(o => o.StoragePurge.EnablePurge = false);
    }

    /// <summary>
    /// Creates a scoped <see cref="IDatabaseSchema"/> from the pipeline and
    /// runs schema migration. Call after <see cref="IdentityServerPipeline.Initialize"/>.
    /// </summary>
    public static async Task MigrateStorageSchemaAsync(this IdentityServerPipeline pipeline)
    {
        await using var scope = pipeline.ApplicationServices.CreateAsyncScope();
        var schema = scope.ServiceProvider.GetRequiredService<IDatabaseSchema>();
        await schema.MigrateAsync(TestContext.Current.CancellationToken);
    }
}
