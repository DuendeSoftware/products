// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Configuration.EntityFramework;
using Duende.IdentityServer.EntityFramework.Options;
using Duende.IdentityServer.EntityFramework.Storage;
using Duende.IdentityServer.IntegrationTests.Common;
using Duende.IdentityServer.IntegrationTests.TestFramework;
using Duende.IdentityServer.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.IdentityServer.IntegrationTests.TestHosts;

public class ConfigurationHost : GenericHost
{
    public ConfigurationHost(
        InMemoryDatabaseRoot databaseRoot,
        string baseAddress = "https://configuration")
            : base(baseAddress)
    {
        OnConfigureServices += (services) => ConfigureServices(services, databaseRoot);
        OnConfigure += Configure;
    }

    private void ConfigureServices(IServiceCollection services, InMemoryDatabaseRoot databaseRoot)
    {
        services.AddRouting();
        services.AddAuthorization();

        services.AddSingleton<ICancellationTokenProvider, MockCancellationTokenProvider>();

        services.AddIdentityServerConfiguration(opt =>
            {

            })
            .AddClientConfigurationStore();
        services.AddSingleton(new ConfigurationStoreOptions());
        services.AddConfigurationDbContext(options =>
        {
            options.ConfigureDbContext = b =>
                b.UseInMemoryDatabase("configurationDb", databaseRoot);
        });
    }

    private void Configure(WebApplication app)
    {
        app.UseRouting();
        app.UseAuthorization();
        app.MapDynamicClientRegistration("/connect/dcr")
            .AllowAnonymous();
    }
}
