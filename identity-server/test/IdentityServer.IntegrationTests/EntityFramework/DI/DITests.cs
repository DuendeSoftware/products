// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EntityFramework.IntegrationTests.DI;

public class DITests
{
    [Fact]
    public void AddConfigurationStore_on_empty_builder_should_not_throw()
    {
        var services = new ServiceCollection();
        services.AddIdentityServerBuilder()
            .AddConfigurationStore(options => options.ConfigureDbContext = b => b.UseInMemoryDatabase(Guid.NewGuid().ToString()));
    }
}
