// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.DataExtensions.Internal;
using Duende.Storage.Internal;
using Duende.Storage.Sqlite;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.Spaces;

public sealed class SpaceSchemaRegistrationOrderTests
{
    [Fact]
    public void when_database_schemas_are_added_after_spaces_expect_startup_to_fail_with_the_order_to_use()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        TestSpacesLicense.RegisterEntitled(services);
        services.AddStorageInternal(builder => builder.AddSqliteInMemory());
        services.AddIdentityServerBuilder().AddSpaces(_ => { });
        services.AddDynamicSchemaStorage();
        using var serviceProvider = services.BuildServiceProvider();
        var app = new ApplicationBuilder(serviceProvider);

        var exception = Should.Throw<InvalidOperationException>(() => app.UseSpaceResolution());

        exception.Message.ShouldBe(
            "ISchemaAdmin was registered after AddSpaces(). Call AddDynamicSchemas() before AddSpaces(), so the Space schema is stored in the management space.");
    }

    [Fact]
    public void when_database_schemas_are_added_before_spaces_expect_startup_to_succeed()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        TestSpacesLicense.RegisterEntitled(services);
        services.AddStorageInternal(builder => builder.AddSqliteInMemory());
        services.AddDynamicSchemaStorage();
        services.AddIdentityServerBuilder().AddSpaces(_ => { });
        using var serviceProvider = services.BuildServiceProvider();
        var app = new ApplicationBuilder(serviceProvider);

        Should.NotThrow(() => app.UseSpaceResolution());
    }
}
