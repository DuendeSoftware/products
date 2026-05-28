// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable


using Duende.IdentityServer.EntityFramework.DbContexts;
using Duende.IdentityServer.EntityFramework.Mappers;
using Duende.IdentityServer.EntityFramework.Options;
using Duende.IdentityServer.EntityFramework.Stores;
using Duende.IdentityServer.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Duende.IdentityServer.IntegrationTests.EntityFramework.Storage.Stores;

/// <summary>
/// A custom identity provider subtype used to verify that the store can
/// reconstruct user-defined derived types via the factory.
/// </summary>
internal record TestCustomProvider : IdentityProvider
{
    public TestCustomProvider() : base("test-custom") { }
    public TestCustomProvider(IdentityProvider other) : base("test-custom", other) { }

    public string? CustomProperty
    {
        get => this["CustomProperty"];
        set => this["CustomProperty"] = value;
    }
}

/// <summary>
/// Test implementation of <see cref="IIdentityProviderFactory"/> that handles
/// the built-in "oidc" and "saml" provider types as well as a custom type.
/// </summary>
internal class TestIdentityProviderFactory : IIdentityProviderFactory
{
    public IdentityProvider? Create(IdentityProvider baseModel) => baseModel.Type switch
    {
        "oidc" => new OidcProvider(baseModel),
        "saml" => new SamlProvider(baseModel),
        "test-custom" => new TestCustomProvider(baseModel),
        _ => null
    };
}

public class IdentityProviderStoreTests : IntegrationTest<IdentityProviderStoreTests, ConfigurationDbContext, ConfigurationStoreOptions>
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    public IdentityProviderStoreTests(DatabaseProviderFixture<ConfigurationDbContext> fixture) : base(fixture)
    {
        foreach (var options in TestDatabaseProviders)
        {
            using var context = new ConfigurationDbContext(options);
            context.Database.EnsureCreated();
        }
    }



    [Theory, MemberData(nameof(TestDatabaseProviders))]
    public async Task GetBySchemeAsync_should_find_by_scheme(DbContextOptions<ConfigurationDbContext> options)
    {
        await using (var context = new ConfigurationDbContext(options))
        {
            var idp = new OidcProvider
            {
                Scheme = "scheme1",
                Type = "oidc"
            };
            context.IdentityProviders.Add(idp.ToEntity());
            await context.SaveChangesAsync();
        }

        await using (var context = new ConfigurationDbContext(options))
        {
            var store = new IdentityProviderStore(context, new NullLogger<IdentityProviderStore>(), new TestIdentityProviderFactory());
            var item = await store.GetBySchemeAsync("scheme1", _ct);

            item.ShouldNotBeNull();
        }
    }


    [Theory, MemberData(nameof(TestDatabaseProviders))]
    public async Task GetBySchemeAsync_should_filter_by_type(DbContextOptions<ConfigurationDbContext> options)
    {
        await using (var context = new ConfigurationDbContext(options))
        {
            var idp = new OidcProvider
            {
                Scheme = "scheme2",
                Type = "unknown"
            };
            context.IdentityProviders.Add(idp.ToEntity());
            await context.SaveChangesAsync();
        }

        await using (var context = new ConfigurationDbContext(options))
        {
            var store = new IdentityProviderStore(context, new NullLogger<IdentityProviderStore>(), new TestIdentityProviderFactory());
            var item = await store.GetBySchemeAsync("scheme2", _ct);

            item.ShouldBeNull();
        }
    }

    [Theory, MemberData(nameof(TestDatabaseProviders))]
    public async Task GetBySchemeAsync_should_return_saml_provider(DbContextOptions<ConfigurationDbContext> options)
    {
        await using (var context = new ConfigurationDbContext(options))
        {
            var idp = new SamlProvider
            {
                Scheme = "saml-scheme",
                IdpEntityId = "https://idp.example.com",
                SingleSignOnServiceUrl = "https://idp.example.com/sso"
            };
            context.IdentityProviders.Add(idp.ToEntity());
            await context.SaveChangesAsync();
        }

        await using (var context = new ConfigurationDbContext(options))
        {
            var store = new IdentityProviderStore(context, new NullLogger<IdentityProviderStore>(), new TestIdentityProviderFactory());
            var item = await store.GetBySchemeAsync("saml-scheme", _ct);

            item.ShouldNotBeNull();
            item.ShouldBeOfType<SamlProvider>();
            var saml = (SamlProvider)item;
            saml.IdpEntityId.ShouldBe("https://idp.example.com");
            saml.SingleSignOnServiceUrl.ShouldBe("https://idp.example.com/sso");
        }
    }

    [Theory, MemberData(nameof(TestDatabaseProviders))]
    public async Task GetBySchemeAsync_should_filter_by_scheme_casing(DbContextOptions<ConfigurationDbContext> options)
    {
        await using (var context = new ConfigurationDbContext(options))
        {
            var idp = new OidcProvider
            {
                Scheme = "SCHEME3",
                Type = "oidc"
            };
            context.IdentityProviders.Add(idp.ToEntity());
            await context.SaveChangesAsync();
        }

        await using (var context = new ConfigurationDbContext(options))
        {
            var store = new IdentityProviderStore(context, new NullLogger<IdentityProviderStore>(), new TestIdentityProviderFactory());
            var item = await store.GetBySchemeAsync("scheme3", _ct);

            item.ShouldBeNull();
        }
    }

    [Theory, MemberData(nameof(TestDatabaseProviders))]
    public async Task GetBySchemeAsync_should_return_custom_provider(DbContextOptions<ConfigurationDbContext> options)
    {
        await using (var context = new ConfigurationDbContext(options))
        {
            var idp = new TestCustomProvider
            {
                Scheme = "custom-scheme",
                CustomProperty = "custom-value"
            };
            context.IdentityProviders.Add(idp.ToEntity());
            await context.SaveChangesAsync();
        }

        await using (var context = new ConfigurationDbContext(options))
        {
            var store = new IdentityProviderStore(context, new NullLogger<IdentityProviderStore>(), new TestIdentityProviderFactory());
            var item = await store.GetBySchemeAsync("custom-scheme", _ct);

            item.ShouldNotBeNull();
            item.ShouldBeOfType<TestCustomProvider>();
            var custom = (TestCustomProvider)item;
            custom.CustomProperty.ShouldBe("custom-value");
        }
    }
}
