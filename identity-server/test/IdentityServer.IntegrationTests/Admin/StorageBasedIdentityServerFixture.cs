// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Admin;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.IntegrationTests.TestFramework;
using Duende.IdentityServer.IntegrationTests.TestFramework.TestIsolation;
using Duende.IdentityServer.Models;
using Duende.Storage.Schema;
using Duende.Storage.Sqlite;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.IdentityServer.IntegrationTests.Admin;

/// <summary>
/// Per-test fixture that stands up a full IdentityServer with SQLite-backed configuration
/// storage. Use this fixture to verify end-to-end token flows where clients are created
/// via <see cref="IClientAdmin"/> and then consumed by the live token endpoint.
/// </summary>
public sealed class StorageBasedIdentityServerFixture : IAsyncLifetime
{
    private readonly WebServerFixture _webServerFixture;
    private readonly string _dbName = $"e2e_{Guid.NewGuid():N}";
    private KestrelBasedTestServer? _server;
    private IServiceScope? _adminScope;

    public StorageBasedIdentityServerFixture(WebServerFixture webServerFixture) =>
        _webServerFixture = webServerFixture;

    /// <summary>
    /// Pre-built <see cref="HttpClient"/> targeting the IdentityServer token endpoint.
    /// Available after <see cref="InitializeAsync"/>.
    /// </summary>
    public HttpClient HttpClient => _server!.Client;

    public Action<IServiceCollection> ConfigureServices = _ => { };
    public Action<IIdentityServerBuilder> ConfigureIdentityServer = _ => { };
    public Action<IdentityServerOptions> ConfigureIdentityServerOptions = _ => { };
    public Action<WebAppWrapper> ConfigureApp = _ => { };

    /// <summary>
    /// A long-lived <see cref="IClientAdmin"/> scoped to this fixture instance.
    /// Available after <see cref="InitializeAsync"/>.
    /// </summary>
    public IClientAdmin ClientAdmin => _adminScope!.ServiceProvider.GetRequiredService<IClientAdmin>();

    public async ValueTask InitializeAsync()
    {
        var ct = TestContext.Current.CancellationToken;

        _server = new KestrelBasedTestServer(
            "identity-server",
            _webServerFixture,
            new PrefixedTestOutputHelper(TestContext.Current.TestOutputHelper!, "identity-server"),
            services =>
            {
                ConfigureServices(services);
                services.AddRouting();

                // Todo: this should be made available differently 
                var identityServer = services.AddIdentityServer(options =>
                    {
                        options.EmitStaticAudienceClaim = true;
                        ConfigureIdentityServerOptions(options);
                    })
                    .AddStorage(storage =>
                        storage.AddSqlite(opt =>
                            opt.ConnectionString = $"Data Source={_dbName};Mode=Memory;Cache=Shared"))
                    .AddConfigurationStorage()
                    .AddOperationalStorage()
                    .AddInMemoryDataExtensionSchemas([])

                    // Overriding the storage-backed IResourceStore with an in-memory one is an
                    // intentional, whole-store replacement (not a partial category mix): both
                    // required resource collections (API scopes and identity resources) are
                    // supplied together so the in-memory IResourceStore has everything this
                    // fixture's tests need.
                    .AddInMemoryApiScopes([new ApiScope("scope1", "Scope 1")])
                    .AddInMemoryIdentityResources([new Models.IdentityResources.OpenId()])
                    .AddJwtBearerClientAuthentication();

                ConfigureIdentityServer(identityServer);
            },
            webapp =>
            {
                ConfigureApp(webapp);
                webapp.UseIdentityServer();
            });

        await _server.StartAsync();

        // Run schema migration
        var schema = _server.GetRequiredService<IStorageInstanceSchema>();
        await schema.MigrateAsync(ct);

        // Create a persistent admin scope for this fixture's lifetime
        _adminScope = _server.Services.CreateScope();

        // Seed the storage-backed API scope/identity resource that back the in-memory
        // IResourceStore configured above, so IClientAdmin's allowed-scope existence check
        // (which reads from storage) accepts "scope1" and "openid" the way existing tests expect.
        // Uses the internal repositories directly rather than IApiScopeAdmin/IIdentityResourceAdmin,
        // which are registered as unsupported stubs when an in-memory resource store is configured.
        var apiScopeRepository = _adminScope.ServiceProvider.GetRequiredService<Duende.IdentityServer.Stores.Storage.ApiScopes.ApiScopeRepository>();
        var scope1Id = Duende.Storage.UuidV7.New();
        await apiScopeRepository.CreateAsync(scope1Id, new Duende.IdentityServer.Stores.Storage.ApiScopeDso.V1
        {
            Id = scope1Id.Value,
            Name = "scope1",
            Enabled = true,
            ShowInDiscoveryDocument = true,
            Required = false,
            Emphasize = false,
            UserClaims = []
        }, ct);
        var identityResourceRepository = _adminScope.ServiceProvider.GetRequiredService<Duende.IdentityServer.Stores.Storage.IdentityResources.IdentityResourceRepository>();
        var openidId = Duende.Storage.UuidV7.New();
        await identityResourceRepository.CreateAsync(openidId, new Duende.IdentityServer.Stores.Storage.IdentityResourceDso.V1
        {
            Id = openidId.Value,
            Name = "openid",
            Enabled = true,
            ShowInDiscoveryDocument = true,
            Required = false,
            Emphasize = false,
            UserClaims = []
        }, ct);
    }

    public Uri BuildUri(string path) => _server!.BuildUrl(path);
    public Uri BaseAddress => _server!.BaseAddress;

    public T GetRequiredService<T>() where T : class => _server!.GetRequiredService<T>();


    public async ValueTask DisposeAsync()
    {
        _adminScope?.Dispose();
        _adminScope = null;

        if (_server is not null)
        {
            await _server.DisposeAsync();
            _server = null;
        }
    }
}
