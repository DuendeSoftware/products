// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Admin;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.IntegrationTests.TestFramework;
using Duende.IdentityServer.IntegrationTests.TestFramework.TestIsolation;
using Duende.IdentityServer.Models;
using Duende.Storage.Internal;
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
                services.AddStorageInternal(storage =>
                    storage.AddSqliteStore(opt =>
                        opt.ConnectionString = $"Data Source={_dbName};Mode=Memory;Cache=Shared"));

                var identityServer = services.AddIdentityServer(options =>
                    {
                        options.EmitStaticAudienceClaim = true;
                        ConfigureIdentityServerOptions(options);
                    })
                    .AddConfigurationStorage()
                    .AddOperationalStorage()
                    .AddInMemoryDataExtensionSchemas([])

                    // Todo: this should not be possible when storage is enabled
                    .AddInMemoryApiScopes([new ApiScope("scope1", "Scope 1")])
                    .AddInMemoryIdentityResources([new Models.IdentityResources.OpenId()]);

                ConfigureIdentityServer(identityServer);
            },
            webapp =>
            {
                ConfigureApp(webapp);
                webapp.UseIdentityServer();
            });

        await _server.StartAsync();

        // Run schema migration
        var schema = _server.GetRequiredService<IDatabaseSchema>();
        await schema.MigrateAsync(ct);

        // Create a persistent admin scope for this fixture's lifetime
        _adminScope = _server.Services.CreateScope();
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
