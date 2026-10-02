// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Admin;
using Duende.IdentityServer.Stores;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.IdentityServer.IntegrationTests.EntityFramework.DI;

public class DITests
{
    [Fact]
    public void AddEntityFrameworkConfigurationStore_on_empty_builder_should_not_throw()
    {
        var services = new ServiceCollection();
        services.AddIdentityServerBuilder()
            .AddEntityFrameworkConfigurationStore(options => options.ConfigureDbContext = b => b.UseInMemoryDatabase(Guid.NewGuid().ToString()));
    }

    [Fact]
    public void AddEntityFrameworkConfigurationStore_disables_all_six_config_admins()
    {
        var services = new ServiceCollection();
        services.AddIdentityServerBuilder()
            .AddEntityFrameworkConfigurationStore(options => options.ConfigureDbContext = b => b.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        using var sp = services.BuildServiceProvider();
        using var scope = sp.CreateScope();

        Should.Throw<NotSupportedException>(() => scope.ServiceProvider.GetService<IClientAdmin>());
        Should.Throw<NotSupportedException>(() => scope.ServiceProvider.GetService<IIdentityResourceAdmin>());
        Should.Throw<NotSupportedException>(() => scope.ServiceProvider.GetService<IApiResourceAdmin>());
        Should.Throw<NotSupportedException>(() => scope.ServiceProvider.GetService<IApiScopeAdmin>());
        Should.Throw<NotSupportedException>(() => scope.ServiceProvider.GetService<IIdentityProviderAdmin>());
        Should.Throw<NotSupportedException>(() => scope.ServiceProvider.GetService<ISamlServiceProviderAdmin>());
    }

#pragma warning disable CS0618 // Type or member is obsolete
    [Fact]
    public void AddOperationalStore_forwards_to_AddEntityFrameworkOperationalStore()
    {
        var services = new ServiceCollection();
        services.AddIdentityServerBuilder()
            .AddOperationalStore(options => options.ConfigureDbContext = b => b.UseInMemoryDatabase(Guid.NewGuid().ToString()));

        services.Any(d => d.ServiceType == typeof(IPersistedGrantStore)).ShouldBeTrue();
    }
#pragma warning restore CS0618
}
