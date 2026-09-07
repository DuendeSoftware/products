// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Admin;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.IdentityServer.IntegrationTests.EntityFramework.DI;

public class DITests
{
    [Fact]
    public void AddConfigurationStore_on_empty_builder_should_not_throw()
    {
        var services = new ServiceCollection();
        services.AddIdentityServerBuilder()
            .AddConfigurationStore(options => options.ConfigureDbContext = b => b.UseInMemoryDatabase(Guid.NewGuid().ToString()));
    }

    [Fact]
    public void AddConfigurationStore_disables_all_six_config_admins()
    {
        var services = new ServiceCollection();
        services.AddIdentityServerBuilder()
            .AddConfigurationStore(options => options.ConfigureDbContext = b => b.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        using var sp = services.BuildServiceProvider();
        using var scope = sp.CreateScope();

        Should.Throw<NotSupportedException>(() => scope.ServiceProvider.GetService<IClientAdmin>());
        Should.Throw<NotSupportedException>(() => scope.ServiceProvider.GetService<IIdentityResourceAdmin>());
        Should.Throw<NotSupportedException>(() => scope.ServiceProvider.GetService<IApiResourceAdmin>());
        Should.Throw<NotSupportedException>(() => scope.ServiceProvider.GetService<IApiScopeAdmin>());
        Should.Throw<NotSupportedException>(() => scope.ServiceProvider.GetService<IIdentityProviderAdmin>());
        Should.Throw<NotSupportedException>(() => scope.ServiceProvider.GetService<ISamlServiceProviderAdmin>());
    }
}
