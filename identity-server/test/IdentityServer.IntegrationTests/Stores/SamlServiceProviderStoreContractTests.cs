// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;

namespace Duende.IdentityServer.IntegrationTests.Stores;

/// <summary>
/// Abstract contract tests for ISamlServiceProviderStore implementations.
/// Each derived class provides a specific store backend (EF, Storage, InMemory).
/// </summary>
public abstract class SamlServiceProviderStoreContractTests : IAsyncLifetime
{
    protected readonly Ct _ct = TestContext.Current.CancellationToken;

    /// <summary>
    /// Creates a store instance that operates against the shared test database.
    /// Callers must dispose the returned <see cref="StoreHandle{T}"/> after use.
    /// </summary>
    protected abstract StoreHandle<ISamlServiceProviderStore> CreateStore();

    /// <summary>
    /// Creates a store instance with guaranteed empty state (no previously seeded data).
    /// Callers must dispose the returned <see cref="StoreHandle{T}"/> after use.
    /// </summary>
    protected abstract Task<StoreHandle<ISamlServiceProviderStore>> CreateIsolatedStoreAsync();

    /// <summary>
    /// Seeds a SAML service provider into the backing store.
    /// </summary>
    protected abstract Task SeedSamlServiceProviderAsync(SamlServiceProvider sp);

    /// <summary>
    /// Lifecycle hook for derived classes that need async initialization.
    /// </summary>
    public virtual ValueTask InitializeAsync() => ValueTask.CompletedTask;

    /// <summary>
    /// Lifecycle hook for derived classes that need async cleanup.
    /// </summary>
    public virtual ValueTask DisposeAsync() => ValueTask.CompletedTask;

    protected static X509Certificate2 CreateTestCertificate()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=Test SP Certificate",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
    }

    [Fact]
    public async Task FindByEntityIdAsync_WhenSPDoesNotExist_ExpectNull()
    {
        await using var handle = CreateStore();
        var sp = await handle.Store.FindByEntityIdAsync("https://nonexistent.example.com", _ct);
        sp.ShouldBeNull();
    }

    [Fact]
    public async Task FindByEntityIdAsync_WhenSPExists_ExpectSPReturned()
    {
        var testSp = new SamlServiceProvider
        {
            EntityId = $"https://sp-exists-{Guid.NewGuid():N}.example.com",
            DisplayName = "Test SP"
        };

        await SeedSamlServiceProviderAsync(testSp);

        await using var handle = CreateStore();
        var sp = await handle.Store.FindByEntityIdAsync(testSp.EntityId, _ct);

        sp.ShouldNotBeNull();
        sp.EntityId.ShouldBe(testSp.EntityId);
        sp.DisplayName.ShouldBe(testSp.DisplayName);
    }

    [Fact]
    public async Task FindByEntityIdAsync_WhenSPExistsWithCollections_ExpectCollectionsReturned()
    {
        using var cert = CreateTestCertificate();

        var testSp = new SamlServiceProvider
        {
            EntityId = $"https://sp-collections-{Guid.NewGuid():N}.example.com",
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

        await SeedSamlServiceProviderAsync(testSp);

        await using var handle = CreateStore();
        var sp = await handle.Store.FindByEntityIdAsync(testSp.EntityId, _ct);

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
        await using var handle = await CreateIsolatedStoreAsync();

        var sps = new List<SamlServiceProvider>();
        await foreach (var sp in handle.Store.GetAllSamlServiceProvidersAsync(_ct))
        {
            sps.Add(sp);
        }

        sps.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetAllSamlServiceProvidersAsync_WhenSPsExist_ExpectAllReturned()
    {
        var sp1 = new SamlServiceProvider { EntityId = $"https://enum-sp1-{Guid.NewGuid():N}.example.com", DisplayName = "Enum SP 1" };
        var sp2 = new SamlServiceProvider { EntityId = $"https://enum-sp2-{Guid.NewGuid():N}.example.com", DisplayName = "Enum SP 2" };

        await SeedSamlServiceProviderAsync(sp1);
        await SeedSamlServiceProviderAsync(sp2);

        await using var handle = CreateStore();
        var sps = new List<SamlServiceProvider>();
        await foreach (var sp in handle.Store.GetAllSamlServiceProvidersAsync(_ct))
        {
            sps.Add(sp);
        }

        sps.ShouldContain(s => s.EntityId == sp1.EntityId);
        sps.ShouldContain(s => s.EntityId == sp2.EntityId);
    }
}
