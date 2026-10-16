// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Spaces.Internal;
using Duende.Storage.Internal;
using Duende.Storage.Schema;
using Duende.Storage.Sqlite;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Duende.Spaces;

public sealed class AddSpacesHowToTests
{
    private static ServiceCollection NewServicesWithSpaces()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSpacesInternal();
        TestSpacesLicense.RegisterEntitled(services);
        services.AddStorageInternal(storage => storage.AddSqliteInMemory());
        return services;
    }

    private static async Task MigrateAsync(IServiceProvider provider, Ct ct)
    {
        var schema = provider.GetRequiredService<IStorageInstanceSchema>();
        await schema.MigrateAsync(ct);
    }

    [Fact]
    public async Task services_resolve_and_a_space_can_be_created_after_migration()
    {
        var ct = TestContext.Current.CancellationToken;
        var services = NewServicesWithSpaces();
        await using var provider = services.BuildServiceProvider();

        await MigrateAsync(provider, ct);

        var admin = provider.GetRequiredService<ISpaceAdmin>();
        var created = await admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "Contoso",
                MatchPatterns = [new SpaceMatchPattern { Path = "/contoso" }]
            },
            ct);

        created.IsSuccess.ShouldBeTrue($"Create failed: {created}");
    }

#pragma warning disable IDE0022 // Use expression body for method: a block body is required so a begin-snippet/end-snippet pair can wrap only the statements, not the signature.
    private static void ConfigureOptions(IServiceCollection services)
    {
        // begin-snippet: add-spaces-configure-options
        services.Configure<SpacesOptions>(spaces =>
        {
            spaces.SpacePathPrefix = "/t";
            spaces.FallbackToDefault = true;
            spaces.LocalCacheExpiration = TimeSpan.FromSeconds(30);
            spaces.Expiration = TimeSpan.FromMinutes(30);
        });
        // end-snippet
    }
#pragma warning restore IDE0022

    [Fact]
    public void options_apply_to_SpacesOptions()
    {
        var services = NewServicesWithSpaces();
        ConfigureOptions(services);
        using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<SpacesOptions>>().Value;

        options.SpacePathPrefix.ShouldBe(new PathString("/t"));
        options.FallbackToDefault.ShouldBeTrue();
        options.LocalCacheExpiration.ShouldBe(TimeSpan.FromSeconds(30));
        options.Expiration.ShouldBe(TimeSpan.FromMinutes(30));
    }

#pragma warning disable IDE0022 // Use expression body for method: a block body is required so a begin-snippet/end-snippet pair can wrap only the statements, not the signature.
    private static void ConfigurePipeline(WebApplication app)
    {
        // begin-snippet: add-spaces-use-space-resolution
        app.UseSpaceResolution();
        app.UseRouting();
        app.UseAuthentication();
        // end-snippet
    }
#pragma warning restore IDE0022

    [Fact]
    public async Task TestServer_request_resolves_a_space()
    {
        var ct = TestContext.Current.CancellationToken;

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddLogging();
        builder.Services.AddSpacesInternal();
        TestSpacesLicense.RegisterEntitled(builder.Services);
        builder.Services.AddStorageInternal(storage => storage.AddSqliteInMemory());
        builder.Services.AddAuthentication().AddCookie();

        var app = builder.Build();
        await MigrateAsync(app.Services, ct);

        var admin = app.Services.GetRequiredService<ISpaceAdmin>();
        var created = await admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "Acme",
                MatchPatterns = [new SpaceMatchPattern { Path = "/acme" }]
            },
            ct);
        created.IsSuccess.ShouldBeTrue($"Create failed: {created}");

        ConfigurePipeline(app);
        app.MapGet("/api/info", (ISpaceContextAccessor spaceContext) => spaceContext.GetSpaceId().Value.ToString());

        await app.StartAsync(ct);
        using var client = app.GetTestClient();

        var response = await client.GetAsync("/t/acme/api/info", ct);
        response.EnsureSuccessStatusCode();
        var spaceId = await response.Content.ReadAsStringAsync(ct);
        spaceId.ShouldBe(created.Id!.Value.ToString());

        await app.DisposeAsync();
    }

    [Fact]
    public void registering_Spaces_twice_throws()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddStorageInternal(storage => storage.AddSqliteInMemory());

        Should.Throw<InvalidOperationException>(() =>
        {
            services.AddSpacesInternal();
            services.AddSpacesInternal();
        });
    }

    [Fact]
    public void UseSpaceResolution_throws_when_an_IDataProtectionProvider_is_registered_after_Spaces()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSpacesInternal();
        TestSpacesLicense.RegisterEntitled(services);

        // Registered after AddSpacesInternal(), so it becomes the effective (last) non-keyed
        // IDataProtectionProvider, and the partitioned one Spaces installed is no longer the last.
        services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());

        using var provider = services.BuildServiceProvider();
        var app = new ApplicationBuilder(provider);

        Should.Throw<InvalidOperationException>(() => app.UseSpaceResolution())
            .Message.ShouldContain("IDataProtectionProvider was registered or replaced");
    }
}
