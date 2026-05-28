// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Xml;
using Duende.IdentityServer.Saml;
using Duende.IdentityServer.Saml.Serialization;
using Duende.IdentityServer.Saml.Xml;

namespace UnitTests.Saml;

public sealed class SamlXmlReaderSignatureResolutionTests
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;
    private const string Category = "SamlXmlReader Signature Resolution";

    [Fact]
    [Trait("Category", Category)]
    public async Task SpFoundWithCertsUsesSpKeysAndDefaultAlgorithms()
    {
        using var cert = CreateSelfSignedCert();
        var reader = new TestableSamlXmlReader
        {
            EntityResolver = (_, _) => Task.FromResult<Saml2Entity?>(new Saml2Entity
            {
                EntityId = "https://sp.example.com",
                SigningKeys = [new SigningKey { Certificate = cert }]
            })
        };
        var traverser = CreateTraverserAtSignature();
        var issuer = new NameId("https://sp.example.com");

        var (keys, algorithms) = await reader.GetSignatureValidationParametersFromIssuerPublicAsync(
            traverser, issuer, _ct);

        keys.ShouldNotBeNull();
        keys.ShouldHaveSingleItem().Certificate.ShouldBe(cert);
        algorithms.ShouldBe(SamlConstants.DefaultAllowedAlgorithms);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task SpFoundWithMultipleCertsReturnsAllKeys()
    {
        using var cert1 = CreateSelfSignedCert();
        using var cert2 = CreateSelfSignedCert();
        var reader = new TestableSamlXmlReader
        {
            EntityResolver = (_, _) => Task.FromResult<Saml2Entity?>(new Saml2Entity
            {
                EntityId = "https://sp.example.com",
                SigningKeys = [new SigningKey { Certificate = cert1 }, new SigningKey { Certificate = cert2 }]
            })
        };
        var traverser = CreateTraverserAtSignature();
        var issuer = new NameId("https://sp.example.com");

        var (keys, algorithms) = await reader.GetSignatureValidationParametersFromIssuerPublicAsync(
            traverser, issuer, _ct);

        keys.ShouldNotBeNull();
        var keyList = keys.ToList();
        keyList.Count.ShouldBe(2);
        keyList[0].Certificate.ShouldBe(cert1);
        keyList[1].Certificate.ShouldBe(cert2);
        algorithms.ShouldBe(SamlConstants.DefaultAllowedAlgorithms);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task ResolverReturnsNullUsesNullParameters()
    {
        using var fallbackCert = CreateSelfSignedCert();
        var reader = new TestableSamlXmlReader
        {
            EntityResolver = (_, _) => Task.FromResult<Saml2Entity?>(null),
            TrustedSigningKeys = [new SigningKey { Certificate = fallbackCert }],
            AllowedAlgorithms = [SignedXml.XmlDsigRSASHA256Url]
        };
        var traverser = CreateTraverserAtSignature();
        var issuer = new NameId("https://sp.example.com");

        var (keys, algorithms) = await reader.GetSignatureValidationParametersFromIssuerPublicAsync(
            traverser, issuer, _ct);

        keys.ShouldBeNull();
        algorithms.ShouldBeNull();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task ResolverReturnsEntityWithNullKeysUsesEntityValues()
    {
        using var fallbackCert = CreateSelfSignedCert();
        var reader = new TestableSamlXmlReader
        {
            EntityResolver = (_, _) => Task.FromResult<Saml2Entity?>(new Saml2Entity
            {
                EntityId = "https://sp.example.com",
                SigningKeys = null
            }),
            TrustedSigningKeys = [new SigningKey { Certificate = fallbackCert }],
            AllowedAlgorithms = [SignedXml.XmlDsigRSASHA256Url]
        };
        var traverser = CreateTraverserAtSignature();
        var issuer = new NameId("https://sp.example.com");

        var (keys, algorithms) = await reader.GetSignatureValidationParametersFromIssuerPublicAsync(
            traverser, issuer, _ct);

        keys.ShouldBeNull();
        algorithms.ShouldBe(SamlConstants.DefaultAllowedAlgorithms);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task NullIssuerWithSignatureAddsError()
    {
        var reader = new TestableSamlXmlReader();
        var traverser = CreateTraverserAtSignature();

        var (keys, algorithms) = await reader.GetSignatureValidationParametersFromIssuerPublicAsync(
            traverser, null, _ct);

        traverser.Errors.Count.ShouldBe(1);
        traverser.Errors[0].Reason.ShouldBe(ErrorReason.MissingElement);
        traverser.Errors[0].LocalName.ShouldBe(SamlConstants.Elements.Issuer);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task NoSignatureReturnsDefaultsWithoutStoreLookup()
    {
        using var fallbackCert = CreateSelfSignedCert();
        var fallbackKeys = new[] { new SigningKey { Certificate = fallbackCert } };
        var fallbackAlgorithms = new[] { SignedXml.XmlDsigRSASHA256Url };
        var reader = new TestableSamlXmlReader
        {
            TrustedSigningKeys = fallbackKeys,
            AllowedAlgorithms = fallbackAlgorithms
        };
        var traverser = CreateTraverserAtNonSignature();
        var issuer = new NameId("https://sp.example.com");

        var (keys, algorithms) = await reader.GetSignatureValidationParametersFromIssuerPublicAsync(
            traverser, issuer, _ct);

        keys.ShouldBe(fallbackKeys);
        algorithms.ShouldBe(fallbackAlgorithms);
        traverser.Errors.ShouldBeEmpty();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task NoSignatureNullDefaultsReturnsNulls()
    {
        var reader = new TestableSamlXmlReader();
        var traverser = CreateTraverserAtNonSignature();
        var issuer = new NameId("https://sp.example.com");

        var (keys, algorithms) = await reader.GetSignatureValidationParametersFromIssuerPublicAsync(
            traverser, issuer, _ct);

        keys.ShouldBeNull();
        algorithms.ShouldBeNull();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task EntityResolverNullFallsBackToTrustedSigningKeys()
    {
        using var fallbackCert = CreateSelfSignedCert();
        var fallbackKeys = new[] { new SigningKey { Certificate = fallbackCert } };
        var fallbackAlgorithms = new[] { SignedXml.XmlDsigRSASHA256Url };
        var reader = new TestableSamlXmlReader
        {
            EntityResolver = null,
            TrustedSigningKeys = fallbackKeys,
            AllowedAlgorithms = fallbackAlgorithms
        };
        var traverser = CreateTraverserAtSignature();
        var issuer = new NameId("https://sp.example.com");

        var (keys, algorithms) = await reader.GetSignatureValidationParametersFromIssuerPublicAsync(
            traverser, issuer, _ct);

        keys.ShouldBe(fallbackKeys);
        algorithms.ShouldBe(fallbackAlgorithms);
        traverser.Errors.ShouldBeEmpty();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task ResolverReturnsEntityWithEmptyKeysUsesEntityValues()
    {
        using var fallbackCert = CreateSelfSignedCert();
        var reader = new TestableSamlXmlReader
        {
            EntityResolver = (_, _) => Task.FromResult<Saml2Entity?>(new Saml2Entity
            {
                EntityId = "https://sp.example.com",
                SigningKeys = []
            }),
            TrustedSigningKeys = [new SigningKey { Certificate = fallbackCert }],
            AllowedAlgorithms = [SignedXml.XmlDsigRSASHA256Url]
        };
        var traverser = CreateTraverserAtSignature();
        var issuer = new NameId("https://sp.example.com");

        var (keys, algorithms) = await reader.GetSignatureValidationParametersFromIssuerPublicAsync(
            traverser, issuer, _ct);

        keys.ShouldNotBeNull();
        keys.ShouldBeEmpty();
        algorithms.ShouldBe(SamlConstants.DefaultAllowedAlgorithms);
    }

    private static XmlTraverser CreateTraverserAtSignature()
    {
        var doc = new XmlDocument();
        var signatureElement = doc.CreateElement(
            "ds", "Signature", SignedXml.XmlDsigNamespaceUrl);
        doc.AppendChild(signatureElement);
        return new XmlTraverser(signatureElement);
    }

    private static XmlTraverser CreateTraverserAtNonSignature()
    {
        var doc = new XmlDocument();
        var element = doc.CreateElement(
            "saml", "Subject", SamlConstants.Namespaces.Assertion);
        doc.AppendChild(element);
        return new XmlTraverser(element);
    }

    private static X509Certificate2 CreateSelfSignedCert()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=Test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return request.CreateSelfSigned(
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddYears(1));
    }

    private sealed class TestableSamlXmlReader : SamlXmlReader
    {
        public Task<(IEnumerable<SigningKey>? trustedSigningKeys, IEnumerable<string>? allowedAlgorithms)>
            GetSignatureValidationParametersFromIssuerPublicAsync(
                XmlTraverser source, NameId? issuer, Ct ct) =>
            GetSignatureValidationParametersFromIssuerAsync(source, issuer, ct);
    }
}
