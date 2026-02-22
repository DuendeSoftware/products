// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.EntityFramework.DbContexts;
using Duende.IdentityServer.EntityFramework.Mappers;
using Duende.IdentityServer.EntityFramework.Options;
using Duende.IdentityServer.EntityFramework.Stores;
using Duende.IdentityServer.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Duende.IdentityServer.IntegrationTests.EntityFramework.Storage.Stores;

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
            var store = new IdentityProviderStore(context, new NullLogger<IdentityProviderStore>());
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
                Type = "saml"
            };
            context.IdentityProviders.Add(idp.ToEntity());
            await context.SaveChangesAsync();
        }

        await using (var context = new ConfigurationDbContext(options))
        {
            var store = new IdentityProviderStore(context, new NullLogger<IdentityProviderStore>());
            var item = await store.GetBySchemeAsync("scheme2", _ct);

            item.ShouldBeNull();
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
            var store = new IdentityProviderStore(context, new NullLogger<IdentityProviderStore>());
            var item = await store.GetBySchemeAsync("scheme3", _ct);

            item.ShouldBeNull();
        }
    }
}
