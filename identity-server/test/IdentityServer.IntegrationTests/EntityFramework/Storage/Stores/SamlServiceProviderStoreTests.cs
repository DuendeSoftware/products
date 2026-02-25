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

public class SamlServiceProviderStoreTests : IntegrationTest<SamlServiceProviderStoreTests, ConfigurationDbContext, ConfigurationStoreOptions>
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    public SamlServiceProviderStoreTests(DatabaseProviderFixture<ConfigurationDbContext> fixture) : base(fixture)
    {
        foreach (var options in TestDatabaseProviders)
        {
            using var context = new ConfigurationDbContext(options);
            context.Database.EnsureCreated();
        }
    }

    [Theory, MemberData(nameof(TestDatabaseProviders))]
    public async Task FindByEntityIdAsync_WhenSpDoesNotExist_ExpectNull(DbContextOptions<ConfigurationDbContext> options)
    {
        await using var context = new ConfigurationDbContext(options);
        var store = new SamlServiceProviderStore(context, new NullLogger<SamlServiceProviderStore>());

        var result = await store.FindByEntityIdAsync("https://notfound.example.com", _ct);

        result.ShouldBeNull();
    }

    [Theory, MemberData(nameof(TestDatabaseProviders))]
    public async Task FindByEntityIdAsync_WhenSpExists_ExpectSpReturned(DbContextOptions<ConfigurationDbContext> options)
    {
        var testSp = new SamlServiceProvider
        {
            EntityId = "https://find-test.example.com",
            DisplayName = "Find Test SP",
            AssertionConsumerServiceUrls = new HashSet<Uri> { new Uri("https://find-test.example.com/acs") }
        };

        await using (var context = new ConfigurationDbContext(options))
        {
            context.SamlServiceProviders.Add(testSp.ToEntity());
            await context.SaveChangesAsync();
        }

        SamlServiceProvider? result;
        await using (var context = new ConfigurationDbContext(options))
        {
            var store = new SamlServiceProviderStore(context, new NullLogger<SamlServiceProviderStore>());
            result = await store.FindByEntityIdAsync(testSp.EntityId, _ct);
        }

        result.ShouldNotBeNull();
        result.EntityId.ShouldBe(testSp.EntityId);
        result.DisplayName.ShouldBe(testSp.DisplayName);
    }

    [Theory, MemberData(nameof(TestDatabaseProviders))]
    public async Task FindByEntityIdAsync_WhenSpExistsWithCollections_ExpectCollectionsReturned(DbContextOptions<ConfigurationDbContext> options)
    {
        var testSp = new SamlServiceProvider
        {
            EntityId = "https://collections-test.example.com",
            DisplayName = "Collections Test SP",
            AssertionConsumerServiceUrls = new HashSet<Uri>
            {
                new Uri("https://collections-test.example.com/acs1"),
                new Uri("https://collections-test.example.com/acs2")
            },
            SingleLogoutServiceUrl = new SamlEndpointType
            {
                Location = new Uri("https://collections-test.example.com/slo"),
                Binding = SamlBinding.HttpPost
            },
            ClaimMappings = new Dictionary<string, string>
            {
                { "department", "businessUnit" },
                { "email", "mail" }
            },
            AssertionConsumerServiceBinding = SamlBinding.HttpPost,
            ClockSkew = TimeSpan.FromMinutes(5),
            RequireSignedAuthnRequests = false,
            Enabled = true
        };

        await using (var context = new ConfigurationDbContext(options))
        {
            context.SamlServiceProviders.Add(testSp.ToEntity());
            await context.SaveChangesAsync();
        }

        SamlServiceProvider? result;
        await using (var context = new ConfigurationDbContext(options))
        {
            var store = new SamlServiceProviderStore(context, new NullLogger<SamlServiceProviderStore>());
            result = await store.FindByEntityIdAsync(testSp.EntityId, _ct);
        }

        result.ShouldNotBeNull();
        result.ShouldSatisfyAllConditions(
            sp => sp.EntityId.ShouldBe(testSp.EntityId),
            sp => sp.DisplayName.ShouldBe(testSp.DisplayName),
            sp => sp.AssertionConsumerServiceUrls.Count.ShouldBe(2),
            sp => sp.AssertionConsumerServiceUrls.ShouldContain(u => u.AbsoluteUri == "https://collections-test.example.com/acs1"),
            sp => sp.AssertionConsumerServiceUrls.ShouldContain(u => u.AbsoluteUri == "https://collections-test.example.com/acs2"),
            sp => sp.SingleLogoutServiceUrl.ShouldNotBeNull(),
            sp => sp.SingleLogoutServiceUrl!.Location.AbsoluteUri.ShouldBe("https://collections-test.example.com/slo"),
            sp => sp.SingleLogoutServiceUrl!.Binding.ShouldBe(SamlBinding.HttpPost),
            sp => sp.ClaimMappings.Count.ShouldBe(2),
            sp => sp.ClaimMappings["department"].ShouldBe("businessUnit"),
            sp => sp.ClaimMappings["email"].ShouldBe("mail"),
            sp => sp.ClockSkew.ShouldBe(TimeSpan.FromMinutes(5))
        );
    }

    [Fact]
    public async Task GetAllSamlServiceProvidersAsync_WhenNoSpsExist_ExpectEmptyCollection()
    {
        var freshOptions = DatabaseProviderBuilder.BuildSqlite<ConfigurationDbContext, ConfigurationStoreOptions>(
            nameof(GetAllSamlServiceProvidersAsync_WhenNoSpsExist_ExpectEmptyCollection), StoreOptions);
        await using var context = new ConfigurationDbContext(freshOptions);
        await context.Database.EnsureCreatedAsync();

        var store = new SamlServiceProviderStore(context, new NullLogger<SamlServiceProviderStore>());

        var result = new List<SamlServiceProvider>();
        await foreach (var sp in store.GetAllSamlServiceProvidersAsync(_ct))
        {
            result.Add(sp);
        }

        result.ShouldNotBeNull();
        result.ShouldBeEmpty();
    }

    [Theory, MemberData(nameof(TestDatabaseProviders))]
    public async Task GetAllSamlServiceProvidersAsync_WhenSpsExist_ExpectAllReturned(DbContextOptions<ConfigurationDbContext> options)
    {
        var testSps = new List<SamlServiceProvider>
        {
            new() { EntityId = "https://enum-sp1.example.com", DisplayName = "Enum SP 1",
                AssertionConsumerServiceUrls = new HashSet<Uri> { new Uri("https://enum-sp1.example.com/acs") } },
            new() { EntityId = "https://enum-sp2.example.com", DisplayName = "Enum SP 2",
                AssertionConsumerServiceUrls = new HashSet<Uri> { new Uri("https://enum-sp2.example.com/acs") } },
            new() { EntityId = "https://enum-sp3.example.com", DisplayName = "Enum SP 3",
                AssertionConsumerServiceUrls = new HashSet<Uri> { new Uri("https://enum-sp3.example.com/acs") } }
        };

        await using (var context = new ConfigurationDbContext(options))
        {
            foreach (var sp in testSps)
            {
                context.SamlServiceProviders.Add(sp.ToEntity());
            }
            await context.SaveChangesAsync();
        }

        await using (var context = new ConfigurationDbContext(options))
        {
            var store = new SamlServiceProviderStore(context, new NullLogger<SamlServiceProviderStore>());

            var result = new List<SamlServiceProvider>();
            await foreach (var sp in store.GetAllSamlServiceProvidersAsync(_ct))
            {
                result.Add(sp);
            }

            result.ShouldNotBeNull();
            result.Count.ShouldBeGreaterThanOrEqualTo(3);
            result.ShouldContain(s => s.EntityId == "https://enum-sp1.example.com");
            result.ShouldContain(s => s.EntityId == "https://enum-sp2.example.com");
            result.ShouldContain(s => s.EntityId == "https://enum-sp3.example.com");
        }
    }

    [Theory, MemberData(nameof(TestDatabaseProviders))]
    public async Task GetAllSamlServiceProvidersAsync_WhenSpsExistWithCollections_ExpectCollectionsIncluded(DbContextOptions<ConfigurationDbContext> options)
    {
        var testSp = new SamlServiceProvider
        {
            EntityId = "https://enum-collections-sp.example.com",
            DisplayName = "Enum Collections SP",
            AssertionConsumerServiceUrls = new HashSet<Uri>
            {
                new Uri("https://enum-collections-sp.example.com/acs")
            },
            ClaimMappings = new Dictionary<string, string>
            {
                { "role", "samlRole" }
            }
        };

        await using (var context = new ConfigurationDbContext(options))
        {
            context.SamlServiceProviders.Add(testSp.ToEntity());
            await context.SaveChangesAsync();
        }

        await using (var context = new ConfigurationDbContext(options))
        {
            var store = new SamlServiceProviderStore(context, new NullLogger<SamlServiceProviderStore>());

            var result = new List<SamlServiceProvider>();
            await foreach (var sp in store.GetAllSamlServiceProvidersAsync(_ct))
            {
                result.Add(sp);
            }

            var found = result.FirstOrDefault(s => s.EntityId == testSp.EntityId);
            found.ShouldNotBeNull();
            found.ShouldSatisfyAllConditions(
                sp => sp.EntityId.ShouldBe(testSp.EntityId),
                sp => sp.AssertionConsumerServiceUrls.Count.ShouldBe(1),
                sp => sp.AssertionConsumerServiceUrls.ShouldContain(u => u.AbsoluteUri == "https://enum-collections-sp.example.com/acs"),
                sp => sp.ClaimMappings.Count.ShouldBe(1),
                sp => sp.ClaimMappings["role"].ShouldBe("samlRole")
            );
        }
    }
}
