// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Cryptography.X509Certificates;
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
    public async Task FindByEntityIdAsync_WhenSPDoesNotExist_ExpectNull(DbContextOptions<ConfigurationDbContext> options)
    {
        await using var context = new ConfigurationDbContext(options);
        var store = new SamlServiceProviderStore(context, new NullLogger<SamlServiceProviderStore>());
        var sp = await store.FindByEntityIdAsync("https://nonexistent.example.com", _ct);
        sp.ShouldBeNull();
    }

    [Theory, MemberData(nameof(TestDatabaseProviders))]
    public async Task FindByEntityIdAsync_WhenSPExists_ExpectSPReturned(DbContextOptions<ConfigurationDbContext> options)
    {
        var testSp = new SamlServiceProvider
        {
            EntityId = "https://sp-exists.example.com",
            DisplayName = "Test SP"
        };

        await using (var context = new ConfigurationDbContext(options))
        {
            context.SamlServiceProviders.Add(testSp.ToEntity());
            await context.SaveChangesAsync(_ct);
        }

        SamlServiceProvider sp;
        await using (var context = new ConfigurationDbContext(options))
        {
            var store = new SamlServiceProviderStore(context, new NullLogger<SamlServiceProviderStore>());
            sp = await store.FindByEntityIdAsync(testSp.EntityId, _ct);
        }

        sp.ShouldNotBeNull();
        sp.EntityId.ShouldBe(testSp.EntityId);
        sp.DisplayName.ShouldBe(testSp.DisplayName);
    }

    [Theory, MemberData(nameof(TestDatabaseProviders))]
    public async Task FindByEntityIdAsync_WhenSPExistsWithCollections_ExpectCollectionsReturned(DbContextOptions<ConfigurationDbContext> options)
    {
        var cert = CreateTestCertificate();

        var testSp = new SamlServiceProvider
        {
            EntityId = "https://sp-collections.example.com",
            DisplayName = "Collections Test SP",
            AssertionConsumerServiceUrls = new HashSet<IndexedEndpoint>
            {
                new IndexedEndpoint { Location = "https://sp-collections.example.com/acs", Binding = SamlBinding.HttpPost }
            },
            SingleLogoutServiceUrls = new HashSet<SamlEndpointType>
            {
                new SamlEndpointType
                {
                    Location = "https://sp-collections.example.com/slo",
                    Binding = SamlBinding.HttpPost
                }
            },
            Certificates = new List<ServiceProviderCertificate>
            {
                new ServiceProviderCertificate { Certificate = cert, Use = KeyUse.Signing },
                new ServiceProviderCertificate { Certificate = cert, Use = KeyUse.Encryption }
            },
            ClaimMappings = new Dictionary<string, string>
            {
                { "department", "businessUnit" }
            },
            ClockSkew = TimeSpan.FromSeconds(30),
            RequestMaxAge = TimeSpan.FromMinutes(5),
            RequireSignedAuthnRequests = true,
            AllowIdpInitiated = false,
            SigningBehavior = SamlSigningBehavior.SignAssertion
        };

        await using (var context = new ConfigurationDbContext(options))
        {
            context.SamlServiceProviders.Add(testSp.ToEntity());
            await context.SaveChangesAsync(_ct);
        }

        SamlServiceProvider sp;
        await using (var context = new ConfigurationDbContext(options))
        {
            var store = new SamlServiceProviderStore(context, new NullLogger<SamlServiceProviderStore>());
            sp = await store.FindByEntityIdAsync(testSp.EntityId, _ct);
        }

        sp.ShouldSatisfyAllConditions(s =>
        {
            s.ShouldNotBeNull();
            s.EntityId.ShouldBe(testSp.EntityId);
            s.DisplayName.ShouldBe(testSp.DisplayName);
            s.AssertionConsumerServiceUrls.Count.ShouldBe(1);
            s.AssertionConsumerServiceUrls.ShouldContain(u => u.Location == "https://sp-collections.example.com/acs");
            s.SingleLogoutServiceUrls.ShouldHaveSingleItem();
            s.SingleLogoutServiceUrls.First().Location.ShouldBe("https://sp-collections.example.com/slo");
            s.SingleLogoutServiceUrls.First().Binding.ShouldBe(SamlBinding.HttpPost);
            s.Certificates.ShouldNotBeNull();
            s.Certificates.Count.ShouldBe(2);
            s.Certificates.ShouldContain(c => c.Use == KeyUse.Signing && c.Certificate.Thumbprint == cert.Thumbprint);
            s.Certificates.ShouldContain(c => c.Use == KeyUse.Encryption && c.Certificate.Thumbprint == cert.Thumbprint);
            s.ClaimMappings.Count.ShouldBe(1);
            s.ClaimMappings["department"].ShouldBe("businessUnit");
            s.ClockSkew.ShouldBe(TimeSpan.FromSeconds(30));
            s.RequestMaxAge.ShouldBe(TimeSpan.FromMinutes(5));
            s.RequireSignedAuthnRequests.ShouldBe(true);
            s.AllowIdpInitiated.ShouldBeFalse();
            s.SigningBehavior.ShouldBe(SamlSigningBehavior.SignAssertion);
        });
    }

    [Fact]
    public async Task GetAllSamlServiceProvidersAsync_WhenNoSPsExist_ExpectEmptyCollection()
    {
        // Use a fresh isolated database so data inserted by other tests doesn't interfere.
        var freshOptions = DatabaseProviderBuilder.BuildSqlite<ConfigurationDbContext, ConfigurationStoreOptions>(
            nameof(GetAllSamlServiceProvidersAsync_WhenNoSPsExist_ExpectEmptyCollection), StoreOptions);
        await using var context = new ConfigurationDbContext(freshOptions);
        await context.Database.EnsureCreatedAsync(_ct);

        var store = new SamlServiceProviderStore(context, new NullLogger<SamlServiceProviderStore>());

        var sps = new List<SamlServiceProvider>();
        await foreach (var sp in store.GetAllSamlServiceProvidersAsync(_ct))
        {
            sps.Add(sp);
        }

        sps.ShouldBeEmpty();
    }

    [Theory, MemberData(nameof(TestDatabaseProviders))]
    public async Task GetAllSamlServiceProvidersAsync_WhenSPsExist_ExpectAllReturned(DbContextOptions<ConfigurationDbContext> options)
    {
        var sp1 = new SamlServiceProvider { EntityId = "https://enum-sp1.example.com", DisplayName = "Enum SP 1" };
        var sp2 = new SamlServiceProvider { EntityId = "https://enum-sp2.example.com", DisplayName = "Enum SP 2" };

        await using (var context = new ConfigurationDbContext(options))
        {
            context.SamlServiceProviders.Add(sp1.ToEntity());
            context.SamlServiceProviders.Add(sp2.ToEntity());
            await context.SaveChangesAsync(_ct);
        }

        await using (var context = new ConfigurationDbContext(options))
        {
            var store = new SamlServiceProviderStore(context, new NullLogger<SamlServiceProviderStore>());

            var sps = new List<SamlServiceProvider>();
            await foreach (var sp in store.GetAllSamlServiceProvidersAsync(_ct))
            {
                sps.Add(sp);
            }

            sps.ShouldContain(s => s.EntityId == "https://enum-sp1.example.com");
            sps.ShouldContain(s => s.EntityId == "https://enum-sp2.example.com");
        }
    }

    private static X509Certificate2 CreateTestCertificate()
    {
        using var rsa = System.Security.Cryptography.RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=Test SP Certificate",
            rsa,
            System.Security.Cryptography.HashAlgorithmName.SHA256,
            System.Security.Cryptography.RSASignaturePadding.Pkcs1);
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
    }
}
