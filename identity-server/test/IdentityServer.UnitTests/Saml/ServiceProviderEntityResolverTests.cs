// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Saml.Serialization;
using Duende.IdentityServer.Stores;

namespace UnitTests.Saml;

public sealed class ServiceProviderEntityResolverTests
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;
    private const string Category = "ServiceProviderEntityResolver";

    [Fact]
    [Trait("Category", Category)]
    public async Task ReturnsEntityWithSigningKeysWhenSpHasCerts()
    {
        using var cert = CreateSelfSignedCert();
        var sp = new SamlServiceProvider
        {
            EntityId = "https://sp.example.com",
            Enabled = true,
            Certificates = [new ServiceProviderCertificate { Certificate = cert, Use = KeyUse.Signing }]
        };
        var store = new InMemorySamlServiceProviderStore([sp]);
        var resolver = new ServiceProviderEntityResolver(store);

        var entity = await resolver.ResolveAsync("https://sp.example.com", _ct);

        entity.ShouldNotBeNull();
        entity.EntityId.ShouldBe("https://sp.example.com");
        entity.SigningKeys.ShouldNotBeNull();
        entity.SigningKeys.ShouldHaveSingleItem().Certificate.ShouldBe(cert);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task ReturnsNullWhenSpNotFound()
    {
        var store = new InMemorySamlServiceProviderStore([]);
        var resolver = new ServiceProviderEntityResolver(store);

        var entity = await resolver.ResolveAsync("https://unknown.example.com", _ct);

        entity.ShouldBeNull();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task ReturnsNullWhenSpHasNoCerts()
    {
        var sp = new SamlServiceProvider
        {
            EntityId = "https://sp.example.com",
            Enabled = true,
            Certificates = null
        };
        var store = new InMemorySamlServiceProviderStore([sp]);
        var resolver = new ServiceProviderEntityResolver(store);

        var entity = await resolver.ResolveAsync("https://sp.example.com", _ct);

        entity.ShouldBeNull();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task ReturnsNullWhenSpHasEmptyCerts()
    {
        var sp = new SamlServiceProvider
        {
            EntityId = "https://sp.example.com",
            Enabled = true,
            Certificates = []
        };
        var store = new InMemorySamlServiceProviderStore([sp]);
        var resolver = new ServiceProviderEntityResolver(store);

        var entity = await resolver.ResolveAsync("https://sp.example.com", _ct);

        entity.ShouldBeNull();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task ReturnsNullWhenSpHasOnlyEncryptionCerts()
    {
        using var cert = CreateSelfSignedCert();
        var sp = new SamlServiceProvider
        {
            EntityId = "https://sp.example.com",
            Enabled = true,
            Certificates = [new ServiceProviderCertificate { Certificate = cert, Use = KeyUse.Encryption }]
        };
        var store = new InMemorySamlServiceProviderStore([sp]);
        var resolver = new ServiceProviderEntityResolver(store);

        var entity = await resolver.ResolveAsync("https://sp.example.com", _ct);

        entity.ShouldBeNull();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task ReturnsMultipleKeysWhenSpHasMultipleCerts()
    {
        using var cert1 = CreateSelfSignedCert();
        using var cert2 = CreateSelfSignedCert();
        var sp = new SamlServiceProvider
        {
            EntityId = "https://sp.example.com",
            Enabled = true,
            Certificates =
            [
                new ServiceProviderCertificate { Certificate = cert1, Use = KeyUse.Signing },
                new ServiceProviderCertificate { Certificate = cert2, Use = KeyUse.Signing }
            ]
        };
        var store = new InMemorySamlServiceProviderStore([sp]);
        var resolver = new ServiceProviderEntityResolver(store);

        var entity = await resolver.ResolveAsync("https://sp.example.com", _ct);

        entity.ShouldNotBeNull();
        var keys = entity.SigningKeys.ShouldNotBeNull().ToList();
        keys.Count.ShouldBe(2);
        keys[0].Certificate.ShouldBe(cert1);
        keys[1].Certificate.ShouldBe(cert2);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task BothUseCertIsIncludedInSigningKeys()
    {
        using var cert = CreateSelfSignedCert();
        var sp = new SamlServiceProvider
        {
            EntityId = "https://sp.example.com",
            Enabled = true,
            Certificates = [new ServiceProviderCertificate { Certificate = cert, Use = KeyUse.Both }]
        };
        var store = new InMemorySamlServiceProviderStore([sp]);
        var resolver = new ServiceProviderEntityResolver(store);

        var entity = await resolver.ResolveAsync("https://sp.example.com", _ct);

        entity.ShouldNotBeNull();
        entity.SigningKeys.ShouldHaveSingleItem().Certificate.ShouldBe(cert);
    }

    private static X509Certificate2 CreateSelfSignedCert()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=Test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return request.CreateSelfSigned(
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddYears(1));
    }
}
