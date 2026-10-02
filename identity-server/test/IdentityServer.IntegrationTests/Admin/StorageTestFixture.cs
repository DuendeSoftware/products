// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Admin;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Hosting;
using Duende.IdentityServer.IntegrationTests.Admin.ApiResources;
using Duende.IdentityServer.IntegrationTests.Admin.ApiScopes;
using Duende.IdentityServer.IntegrationTests.Admin.Clients;
using Duende.IdentityServer.IntegrationTests.Admin.IdentityProviders;
using Duende.IdentityServer.IntegrationTests.Admin.IdentityResources;
using Duende.IdentityServer.IntegrationTests.Admin.SamlServiceProviders;
using Duende.IdentityServer.Saml;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Stores;
using Duende.IdentityServer.Stores.Storage.SamlLogoutSession;
using Duende.IdentityServer.Stores.Storage.SigningKeys;
using Duende.IdentityServer.Validation;
using Duende.Storage.Internal;
using Duende.Storage.Schema;
using Duende.Storage.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
namespace Duende.IdentityServer.IntegrationTests.Admin;

/// <summary>
/// Shared test fixture that wires up IClientAdmin, IClientStore, ISamlServiceProviderAdmin,
/// ISamlServiceProviderStore, ISamlLogoutSessionStore, IDeviceFlowStore, IPushedAuthorizationRequestStore, IServerSideSessionStore, IApiResourceAdmin,
/// IApiScopeAdmin, IIdentityResourceAdmin, IResourceStore, IPersistedGrantStore, and ICorsPolicyService
/// against an in-memory SQLite store. Each fixture instance uses a unique database name to ensure full
/// test isolation.
/// </summary>
public sealed class StorageTestFixture : IAsyncLifetime
{
    private ServiceProvider? _provider;
    private IServiceScope? _scope;

    public IClientAdmin ClientAdmin => _scope!.ServiceProvider.GetRequiredService<IClientAdmin>();
    public IApiResourceAdmin ApiResourceAdmin => _scope!.ServiceProvider.GetRequiredService<IApiResourceAdmin>();
    public IApiScopeAdmin ApiScopeAdmin => _scope!.ServiceProvider.GetRequiredService<IApiScopeAdmin>();
    public IIdentityResourceAdmin IdentityResourceAdmin => _scope!.ServiceProvider.GetRequiredService<IIdentityResourceAdmin>();
    public IClientStore ClientStore => _scope!.ServiceProvider.GetRequiredService<IClientStore>();
    public IResourceStore ResourceStore => _scope!.ServiceProvider.GetRequiredService<IResourceStore>();
    public ICorsPolicyService CorsPolicyService => _scope!.ServiceProvider.GetRequiredService<ICorsPolicyService>();
    public IDeviceFlowStore DeviceFlowStore => _scope!.ServiceProvider.GetRequiredService<IDeviceFlowStore>();
    public IIdentityProviderAdmin IdentityProviderAdmin => _scope!.ServiceProvider.GetRequiredService<IIdentityProviderAdmin>();
    public IIdentityProviderStore IdentityProviderStore => _scope!.ServiceProvider.GetRequiredService<IIdentityProviderStore>();
    public IPushedAuthorizationRequestStore PushedAuthorizationRequestStore => _scope!.ServiceProvider.GetRequiredService<IPushedAuthorizationRequestStore>();
    public ISamlServiceProviderAdmin SamlServiceProviderAdmin => _scope!.ServiceProvider.GetRequiredService<ISamlServiceProviderAdmin>();
    public ISamlServiceProviderStore SamlServiceProviderStore => _scope!.ServiceProvider.GetRequiredService<ISamlServiceProviderStore>();
    public IPersistedGrantStore PersistedGrantStore => _scope!.ServiceProvider.GetRequiredService<IPersistedGrantStore>();
    public ISamlSigninStateStore SamlSigninStateStore => _scope!.ServiceProvider.GetRequiredService<ISamlSigninStateStore>();
    public IServerSideSessionStore ServerSideSessionStore => _scope!.ServiceProvider.GetRequiredService<IServerSideSessionStore>();
    public ISigningKeyStore SigningKeyStore => _scope!.ServiceProvider.GetRequiredService<ISigningKeyStore>();
    internal KeyRepository KeyRepository => _scope!.ServiceProvider.GetRequiredService<KeyRepository>();
    public ISamlLogoutSessionStore SamlLogoutSessionStore => _scope!.ServiceProvider.GetRequiredService<ISamlLogoutSessionStore>();
    public IConnectedApplicationStore ConnectedApplicationStore => _scope!.ServiceProvider.GetRequiredService<IConnectedApplicationStore>();
    public IPartitionedStorageFactory PartitionedStorageFactory => _scope!.ServiceProvider.GetRequiredService<IPartitionedStorageFactory>();
    internal IStorageInstanceRouter StorageInstanceRouter => _scope!.ServiceProvider.GetRequiredService<IStorageInstanceRouter>();
    internal ICrossPartitionStorageFactory CrossPartitionStorageFactory => _scope!.ServiceProvider.GetRequiredService<ICrossPartitionStorageFactory>();
    public IdentityServerOptions Options => _scope!.ServiceProvider.GetRequiredService<IdentityServerOptions>();
    internal ILogger<StoragePurgeHost> StoragePurgeHostLogger => _scope!.ServiceProvider.GetRequiredService<ILogger<StoragePurgeHost>>();
    internal SamlLogoutSessionRepository SamlLogoutSessionRepository => _scope!.ServiceProvider.GetRequiredService<SamlLogoutSessionRepository>();

    public async ValueTask InitializeAsync()
    {
        var ct = TestContext.Current.CancellationToken;

        var services = new ServiceCollection();
        services.AddLogging();

        services.AddIdentityServer()
            .AddStorage(storage =>
                storage.AddSqliteInMemory())
            .AddConfigurationStorage()
            .AddOperationalStorage()
            .AddSamlDynamicProvider()
            .AddClientConfigurationValidator<NopClientConfigurationValidator>()
            .AddIdentityProviderConfigurationValidator<NopIdentityProviderConfigurationValidator>()
            .AddSamlServiceProviderConfigurationValidator<NopSamlServiceProviderConfigurationValidator>()
            .AddInMemoryDataExtensionSchemas([TestClientAttributes.Schema, TestApiResourceAttributes.Schema, TestApiScopeAttributes.Schema, TestIdentityResourceAttributes.Schema, TestIdentityProviderAttributes.Schema, TestSamlServiceProviderAttributes.Schema]);

        // Register the server-side sessions marker manually (without AddServerSideSessions()
        // which would register ServerSideSessionCleanupHost, an IHostedService not needed in tests)
        services.AddSingleton<IServerSideSessionsMarker, NopIServerSideSessionsMarker>();

        _provider = services.BuildServiceProvider();

        // Run schema migration before any tests execute
        var schema = _provider.GetRequiredService<IStorageInstanceSchema>();
        await schema.MigrateAsync(ct);

        _scope = _provider.CreateScope();
    }

    public async ValueTask DisposeAsync()
    {
        _scope?.Dispose();
        _scope = null;

        if (_provider is not null)
        {
            await _provider.DisposeAsync();
            _provider = null;
        }
    }
}

/// <summary>
/// No-op identity provider configuration validator for use in integration test fixtures.
/// Bypasses the default validator that requires provider types to be registered.
/// </summary>
internal sealed class NopIdentityProviderConfigurationValidator : IIdentityProviderConfigurationValidator
{
    public Task ValidateAsync(IdentityProviderConfigurationValidationContext context, Ct ct)
    {
        context.IsValid = true;
        return Task.CompletedTask;
    }
}

/// <summary>
/// No-op SAML SP configuration validator for tests that need to bypass validation rules.
/// </summary>
internal sealed class NopSamlServiceProviderConfigurationValidator : ISamlServiceProviderConfigurationValidator
{
    public Task ValidateAsync(SamlServiceProviderConfigurationValidationContext context, Ct _)
    {
        context.IsValid = true;
        return Task.CompletedTask;
    }
}
