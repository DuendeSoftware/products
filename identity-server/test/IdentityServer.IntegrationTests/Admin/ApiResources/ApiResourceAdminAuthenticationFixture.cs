// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System.Security.Cryptography.X509Certificates;
using Duende.IdentityServer.Admin;
using Duende.IdentityServer.IntegrationTests.Common;
using Duende.IdentityServer.IntegrationTests.TestFramework;
using Duende.IdentityServer.IntegrationTests.TestFramework.TestIsolation;
using Duende.IdentityServer.Stores;
using Duende.Storage.Schema;
using Duende.Storage.Sqlite;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.IdentityServer.IntegrationTests.Admin.ApiResources;

public sealed class ApiResourceAdminAuthenticationFixture : IAsyncLifetime
{
    private readonly WebServerFixture _webServerFixture;
    private readonly string _dbName = $"api_resource_admin_{Guid.NewGuid():N}";
    private KestrelBasedTestServer? _server;
    private IServiceScope? _adminScope;

    public ApiResourceAdminAuthenticationFixture(WebServerFixture webServerFixture) =>
        _webServerFixture = webServerFixture;

    /// <summary>
    /// Pre-built <see cref="HttpClient"/> targeting the IdentityServer introspection endpoint.
    /// Available after <see cref="InitializeAsync"/>.
    /// </summary>
    public HttpClient HttpClient => _server!.Client;

    /// <summary>
    /// A long-lived <see cref="IApiResourceAdmin"/> scoped to this fixture instance.
    /// Available after <see cref="InitializeAsync"/>.
    /// </summary>
    public IApiResourceAdmin ApiResourceAdmin => _adminScope!.ServiceProvider.GetRequiredService<IApiResourceAdmin>();

    /// <summary>
    /// A long-lived <see cref="IResourceStore"/> scoped to this fixture instance, used to
    /// read back API resources and verify secrets round-trip as stored by
    /// <see cref="IApiResourceAdmin.CreateSecretAsync"/>.
    /// </summary>
    public IResourceStore ResourceStore => _adminScope!.ServiceProvider.GetRequiredService<IResourceStore>();

    public async ValueTask InitializeAsync()
    {
        var ct = TestContext.Current.CancellationToken;

        _server = new KestrelBasedTestServer(
            "api-resource-admin",
            _webServerFixture,
            new PrefixedTestOutputHelper(TestContext.Current.TestOutputHelper!, "api-resource-admin"),
            services =>
            {
                services.AddRouting();

                services.AddIdentityServer(options =>
                    {
                        options.EmitStaticAudienceClaim = true;
                        options.KeyManagement.Enabled = false;
                        options.MutualTls.Enabled = true;
                    })
                    .AddStorage(storage =>
                        storage.AddSqlite(opt =>
                            opt.ConnectionString = $"Data Source={_dbName};Mode=Memory;Cache=Shared"))
                    .AddConfigurationStorage()
                    .AddOperationalStorage()
                    .AddInMemoryDataExtensionSchemas([])
                    .AddMutualTlsSecretValidators()
                    .AddJwtBearerClientAuthentication()
                    .AddDeveloperSigningCredential(persistKey: false);
            },
            webapp =>
            {
                webapp.UseMiddleware<MtlsTestMiddleware>();
                webapp.UseIdentityServer();
            });

        await _server.StartAsync();

        var schema = _server.GetRequiredService<IStorageInstanceSchema>();
        await schema.MigrateAsync(ct);

        _adminScope = _server.Services.CreateScope();
    }

    public Uri BuildUri(string path) => _server!.BuildUrl(path);
    public Uri BaseAddress => _server!.BaseAddress;

    public T GetRequiredService<T>() where T : class => _server!.GetRequiredService<T>();

    /// <summary>
    /// Creates an <see cref="HttpClient"/> that presents <paramref name="cert"/> as a simulated
    /// client certificate via <see cref="MtlsMessageHandler"/>, targeting this fixture's server.
    /// Mirrors <c>SpacesMtlsIdentityServerFixture.CreateMtlsClient</c>.
    /// </summary>
    public HttpClient CreateMtlsClient(X509Certificate2 cert)
    {
        ArgumentNullException.ThrowIfNull(cert);

#pragma warning disable CA2000 // Ownership transferred to HttpClient via disposeHandler: true
        var handler = new MtlsMessageHandler(_server!.CreateHandler(), cert);
#pragma warning restore CA2000

#pragma warning disable CA5400 // CRL check intentionally disabled for test infrastructure
        return new HttpClient(handler, disposeHandler: true)
#pragma warning restore CA5400
        {
            BaseAddress = _server.BaseAddress
        };
    }

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
