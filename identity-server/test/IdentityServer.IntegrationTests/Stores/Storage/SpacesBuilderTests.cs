// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.IntegrationTests.TestFramework;
using Duende.Spaces;
using Duende.Storage;
using Duende.Storage.Schema;
using Duende.Storage.Sqlite;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Duende.IdentityServer.IntegrationTests.Stores.Storage;

public class SpacesBuilderTests
{
    [Fact]
    public void Options_set_while_adding_Spaces_are_in_effect()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddIdentityServer()
            .AddSpaces(spaces =>
            {
                spaces.FallbackToDefault = true;
                spaces.SpacePathPrefix = new PathString("/tenants");
            });

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<SpacesOptions>>().Value;

        options.FallbackToDefault.ShouldBeTrue();
        options.SpacePathPrefix.ShouldBe(new PathString("/tenants"));
    }

    [Fact]
    public async Task A_space_can_be_created_when_Spaces_data_is_stored_in_a_named_instance_and_no_default_database_exists()
    {
        var ct = TestContext.Current.CancellationToken;
        var spacesStorageInstanceId = StorageInstanceId.Create("spaces");

        var services = new ServiceCollection();
        services.AddLogging();

        services.AddIdentityServer()
            .AddStorage(spacesStorageInstanceId, spaces => spaces.AddSqliteInMemory($"Spaces_{Guid.NewGuid():N}"))
            .AddSpaces(spacesStorageInstanceId, _ => { });
        SpacesTestLicense.RegisterEntitled(services);

        await using var provider = services.BuildServiceProvider();
        var schema = await provider.GetRequiredService<IStorageInstanceSchemaFactory>()
            .GetStorageInstanceSchema(spacesStorageInstanceId, ct);
        await schema.MigrateAsync(ct);

        var created = await provider.GetRequiredService<ISpaceAdmin>().CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "Contoso",
                MatchPatterns = [new SpaceMatchPattern { Path = "/contoso" }]
            }, ct);
        created.IsSuccess.ShouldBeTrue($"Create failed: {created}");
    }

    [Fact]
    public void Adding_Spaces_twice_throws()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        Should.Throw<InvalidOperationException>(() =>
            services.AddIdentityServer()
                .AddStorage(spaces => spaces.AddSqliteInMemory())
                .AddSpaces(_ => { })
                .AddSpaces(_ => { }));
    }

    [Fact]
    public async Task Adding_the_Entity_Framework_configuration_store_after_Spaces_stops_the_host_from_starting()
    {
        var builder = Host.CreateApplicationBuilder();

        builder.Services.AddIdentityServer()
            .AddStorage(storage => storage.AddSqliteInMemory())
            .AddSpaces(_ => { })
            .AddEntityFrameworkConfigurationStore();

        using var host = builder.Build();

        var exception = await Should.ThrowAsync<OptionsValidationException>(() => host.StartAsync(TestContext.Current.CancellationToken));
        exception.Message.ShouldContain("Spaces");
        exception.Message.ShouldContain("Entity Framework");
    }

    [Fact]
    public async Task Adding_the_Entity_Framework_configuration_store_before_Spaces_stops_the_host_from_starting()
    {
        var builder = Host.CreateApplicationBuilder();

        builder.Services.AddIdentityServer()
            .AddStorage(storage => storage.AddSqliteInMemory())
            .AddEntityFrameworkConfigurationStore()
            .AddSpaces(_ => { });

        using var host = builder.Build();

        var exception = await Should.ThrowAsync<OptionsValidationException>(() => host.StartAsync(TestContext.Current.CancellationToken));
        exception.Message.ShouldContain("Spaces");
        exception.Message.ShouldContain("Entity Framework");
    }

    [Fact]
    public async Task Adding_the_Entity_Framework_operational_store_after_Spaces_stops_the_host_from_starting()
    {
        var builder = Host.CreateApplicationBuilder();

        builder.Services.AddIdentityServer()
            .AddStorage(storage => storage.AddSqliteInMemory())
            .AddSpaces(_ => { })
            .AddEntityFrameworkOperationalStore();

        using var host = builder.Build();

        var exception = await Should.ThrowAsync<OptionsValidationException>(() => host.StartAsync(TestContext.Current.CancellationToken));
        exception.Message.ShouldContain("Spaces");
        exception.Message.ShouldContain("Entity Framework");
    }

    [Fact]
    public async Task Adding_the_Entity_Framework_operational_store_before_Spaces_stops_the_host_from_starting()
    {
        var builder = Host.CreateApplicationBuilder();

        builder.Services.AddIdentityServer()
            .AddStorage(storage => storage.AddSqliteInMemory())
            .AddEntityFrameworkOperationalStore()
            .AddSpaces(_ => { });

        using var host = builder.Build();

        var exception = await Should.ThrowAsync<OptionsValidationException>(() => host.StartAsync(TestContext.Current.CancellationToken));
        exception.Message.ShouldContain("Spaces");
        exception.Message.ShouldContain("Entity Framework");
    }
}
