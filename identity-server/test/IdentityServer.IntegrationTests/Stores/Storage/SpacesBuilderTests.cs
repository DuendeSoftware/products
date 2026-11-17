// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.IntegrationTests.TestFramework;
using Duende.IdentityServer.Models;
using Duende.Spaces;
using Duende.Storage;
using Duende.Storage.Schema;
using Duende.Storage.Sqlite;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
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
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());

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


    [Fact]
    public async Task Default_OIDC_and_SAML_dynamic_provider_caches_start_with_Spaces()
    {
        using var host = CreateDynamicProviderHost(withSpaces: true);

        await Should.NotThrowAsync(() => host.StartAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task A_replaced_dynamic_provider_cache_with_Spaces_stops_the_host_and_names_the_options_type()
    {
        using var host = CreateDynamicProviderHost(withSpaces: true, ReplaceOidcCache);

        var exception = await Should.ThrowAsync<OptionsValidationException>(() => host.StartAsync(TestContext.Current.CancellationToken));
        exception.Failures.ShouldHaveSingleItem().ShouldContain($"IOptionsMonitorCache<{typeof(OpenIdConnectOptions).FullName}>");
    }

    [Fact]
    public async Task A_provider_type_added_without_AddDynamicProvider_with_Spaces_stops_the_host_and_names_the_options_type()
    {
        using var host = CreateDynamicProviderHost(withSpaces: true, AddCustomProviderType);

        var exception = await Should.ThrowAsync<OptionsValidationException>(() => host.StartAsync(TestContext.Current.CancellationToken));
        exception.Failures.ShouldHaveSingleItem().ShouldContain($"IOptionsMonitorCache<{typeof(CustomOptions).FullName}>");
    }

    [Fact]
    public async Task A_dynamic_provider_cache_that_resolves_to_null_with_Spaces_stops_the_host_and_names_the_options_type()
    {
        using var host = CreateDynamicProviderHost(withSpaces: true, services =>
        {
            AddCustomProviderType(services);
            services.AddSingleton<IOptionsMonitorCache<CustomOptions>>(_ => null);
        });

        var exception = await Should.ThrowAsync<OptionsValidationException>(() => host.StartAsync(TestContext.Current.CancellationToken));
        exception.Failures.ShouldHaveSingleItem().ShouldContain($"IOptionsMonitorCache<{typeof(CustomOptions).FullName}>");
    }

    [Fact]
    public async Task A_replaced_dynamic_provider_cache_without_Spaces_starts()
    {
        using var host = CreateDynamicProviderHost(withSpaces: false, ReplaceOidcCache);

        await Should.NotThrowAsync(() => host.StartAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task A_space_path_prefix_with_uppercase_stops_the_host_and_names_the_lowercase_prefix()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddIdentityServer()
            .AddStorage(storage => storage.AddSqliteInMemory())
            .AddSpaces(spaces => spaces.SpacePathPrefix = "/Tenants");
        using var host = builder.Build();

        var exception = await Should.ThrowAsync<OptionsValidationException>(() => host.StartAsync(TestContext.Current.CancellationToken));
        exception.Failures.ShouldHaveSingleItem().ShouldContain("'/tenants'");
    }

    [Fact]
    public async Task An_issuer_uri_with_Spaces_stops_the_host_and_names_the_opt_in()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddIdentityServer(options => options.IssuerUri = "https://issuer.example")
            .AddStorage(storage => storage.AddSqliteInMemory())
            .AddSpaces(_ => { });
        using var host = builder.Build();

        var exception = await Should.ThrowAsync<OptionsValidationException>(() => host.StartAsync(TestContext.Current.CancellationToken));
        var failure = exception.Failures.ShouldHaveSingleItem();
        failure.ShouldContain("IssuerUri");
        failure.ShouldContain("AllowSharedIssuerUri");
    }

    [Fact]
    public async Task An_issuer_uri_with_Spaces_starts_when_a_shared_issuer_is_allowed()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddIdentityServer(options => options.IssuerUri = "https://issuer.example")
            .AddStorage(storage => storage.AddSqliteInMemory())
            .AddSpaces(spaces => spaces.AllowSharedIssuerUri = true);
        using var host = builder.Build();

        await Should.NotThrowAsync(() => host.StartAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task A_whitespace_issuer_uri_with_Spaces_starts_without_the_opt_in()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddIdentityServer(options => options.IssuerUri = " ")
            .AddStorage(storage => storage.AddSqliteInMemory())
            .AddSpaces(_ => { });
        using var host = builder.Build();

        await Should.NotThrowAsync(() => host.StartAsync(TestContext.Current.CancellationToken));
    }

    private sealed class CustomOptions : AuthenticationSchemeOptions;

    private static IHost CreateDynamicProviderHost(bool withSpaces, Action<IServiceCollection> configure = null)
    {
        var builder = Host.CreateApplicationBuilder();

        var identityServer = builder.Services.AddIdentityServer()
            .AddStorage(storage => storage.AddSqliteInMemory())
            .AddSamlDynamicProvider();
        if (withSpaces)
        {
            identityServer.AddSpaces(_ => { });
        }

        configure?.Invoke(builder.Services);

        return builder.Build();
    }

    private static void ReplaceOidcCache(IServiceCollection services) =>
        services.AddSingleton<IOptionsMonitorCache<OpenIdConnectOptions>, OptionsCache<OpenIdConnectOptions>>();

    private static void AddCustomProviderType(IServiceCollection services) =>
        services.Configure<IdentityServerOptions>(options =>
            options.DynamicProviders.AddProviderType<OpenIdConnectHandler, CustomOptions, OidcProvider>("custom"));
}
