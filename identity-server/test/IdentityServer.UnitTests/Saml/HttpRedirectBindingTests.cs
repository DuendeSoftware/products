// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using System.IO.Compression;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Text;
using System.Xml;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Saml;
using Duende.IdentityServer.Saml.Bindings;
using Duende.IdentityServer.Saml.Infrastructure;
using Duende.IdentityServer.Saml.Xml;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace UnitTests.Saml;

public sealed class HttpRedirectBindingTests
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    private const string Category = "SAML HTTP Redirect Binding Security";

    private static readonly Func<string, Ct, Task<Saml2Entity?>> NullResolver =
        (_, _) => Task.FromResult<Saml2Entity?>(null);

    private static HttpRedirectBinding CreateBinding() => new(Options.Create(new IdentityServerOptions()));

    private static Func<string, Ct, Task<Saml2Entity?>> ResolverFor(string entityId, params X509Certificate2[] certs) =>
        (id, _) => Task.FromResult<Saml2Entity?>(
            id == entityId
                ? new Saml2Entity { EntityId = id, SigningKeys = certs.Select(c => new SigningKey { Certificate = c }) }
                : null);

    private static Func<string, Ct, Task<Saml2Entity?>> ResolverWithAllowedAlgorithms(
        string entityId, X509Certificate2 cert, IEnumerable<string> allowedAlgorithms) =>
        (id, _) => Task.FromResult<Saml2Entity?>(
            id == entityId
                ? new Saml2Entity
                {
                    EntityId = id,
                    SigningKeys = [new SigningKey { Certificate = cert }],
                    AllowedAlgorithms = allowedAlgorithms
                }
                : null);

    private static string DeflateAndEncode(string xmlPayload)
    {
        using var compressed = new MemoryStream();
        using (var deflateStream = new DeflateStream(compressed, CompressionLevel.Optimal))
        {
            using var writer = new StreamWriter(deflateStream, Encoding.UTF8);
            writer.Write(xmlPayload);
        }
        return Uri.EscapeDataString(Convert.ToBase64String(compressed.ToArray()));
    }

    private static string CreateRedirectUrl(string xmlPayload, string? relayState = "state123")
    {
        var encoded = DeflateAndEncode(xmlPayload);
        var qs = relayState != null
            ? $"SAMLRequest={encoded}&RelayState={Uri.EscapeDataString(relayState)}"
            : $"SAMLRequest={encoded}";
        return $"https://idp.example.com/saml?{qs}";
    }

    private static string CreateDecompressionBombUrl()
    {
        var largePayload = "<root>" + new string('X', SecureXmlParser.DefaultMaxMessageSize + 1024) + "</root>";
        return CreateRedirectUrl(largePayload);
    }

    private static X509Certificate2 CreateTestCert()
    {
        using var rsa = RSA.Create(2048);
        var req = new CertificateRequest("CN=Test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        req.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, critical: true));
        var cert = req.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
        return X509CertificateLoader.LoadPkcs12(cert.Export(X509ContentType.Pfx), null, X509KeyStorageFlags.Exportable);
    }

    // Signs the canonical redirect content: SAMLRequest=<encoded>[&RelayState=<encoded>]&SigAlg=<encoded>
    private static (string encodedRequest, string signature, string sigAlgEncoded) SignRequest(
        string xmlPayload,
        X509Certificate2 cert,
        string? relayState = null,
        string algorithm = SignedXml.XmlDsigRSASHA256Url)
    {
        var encodedRequest = DeflateAndEncode(xmlPayload);
        var sigAlgEncoded = Uri.EscapeDataString(algorithm);

        var contentToSign = $"SAMLRequest={encodedRequest}";
        if (relayState != null)
        {
            contentToSign += $"&RelayState={Uri.EscapeDataString(relayState)}";
        }
        contentToSign += $"&SigAlg={sigAlgEncoded}";

        using var rsa = cert.GetRSAPrivateKey()!;
        var hashAlg = algorithm == SignedXml.XmlDsigRSASHA384Url ? HashAlgorithmName.SHA384
            : algorithm == SignedXml.XmlDsigRSASHA512Url ? HashAlgorithmName.SHA512
            : HashAlgorithmName.SHA256;

        var sigBytes = rsa.SignData(Encoding.UTF8.GetBytes(contentToSign), hashAlg, RSASignaturePadding.Pkcs1);
        return (encodedRequest, Convert.ToBase64String(sigBytes), sigAlgEncoded);
    }

    private static HttpRequest BuildHttpRequest(string rawQueryString)
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("idp.example.com");
        context.Request.Path = "/Saml2/SSO";
        context.Request.QueryString = new QueryString(rawQueryString.StartsWith('?') ? rawQueryString : "?" + rawQueryString);
        return context.Request;
    }

    private static string SimpleAuthnRequestXml(string issuer = "https://sp.example.com") =>
        $"""<samlp:AuthnRequest xmlns:samlp="urn:oasis:names:tc:SAML:2.0:protocol" xmlns:saml="urn:oasis:names:tc:SAML:2.0:assertion" ID="_test" Version="2.0" IssueInstant="2024-01-01T00:00:00Z"><saml:Issuer>{issuer}</saml:Issuer></samlp:AuthnRequest>""";



    [Fact]
    [Trait("Category", Category)]
    public async Task UnbindWithValidSamlRequestShouldSucceed()
    {
        var binding = CreateBinding();
        var xml = """<samlp:AuthnRequest xmlns:samlp="urn:oasis:names:tc:SAML:2.0:protocol" ID="_test" Version="2.0" IssueInstant="2024-01-01T00:00:00Z"/>""";
        var url = CreateRedirectUrl(xml);

        var result = await binding.UnBindAsync(url, NullResolver, _ct);

        result.Name.ShouldBe("SAMLRequest");
        result.Xml.LocalName.ShouldBe("AuthnRequest");
        result.Binding.ShouldBe("urn:oasis:names:tc:SAML:2.0:bindings:HTTP-Redirect");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task UnbindWithXxePayloadShouldThrowXmlException()
    {
        var binding = CreateBinding();
        var xml = """<?xml version="1.0"?><!DOCTYPE root [<!ENTITY xxe SYSTEM "file:///etc/passwd">]><root>&xxe;</root>""";
        var url = CreateRedirectUrl(xml);

        var ex = await Should.ThrowAsync<XmlException>(() => binding.UnBindAsync(url, NullResolver, _ct));

        ex.Message.ShouldBe(
            "Failed to parse XML document with secure settings. " +
            "The document may contain prohibited constructs (DTD, external entities) or be malformed.");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task UnbindWithDtdPayloadShouldThrowXmlException()
    {
        var binding = CreateBinding();
        var xml = """<?xml version="1.0"?><!DOCTYPE root [<!ELEMENT root ANY>]><root>content</root>""";
        var url = CreateRedirectUrl(xml);

        var ex = await Should.ThrowAsync<XmlException>(() => binding.UnBindAsync(url, NullResolver, _ct));

        ex.Message.ShouldBe(
            "Failed to parse XML document with secure settings. " +
            "The document may contain prohibited constructs (DTD, external entities) or be malformed.");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task UnbindWithOversizedPayloadShouldThrow()
    {
        var binding = CreateBinding();
        var xml = "<root>" + new string('X', SecureXmlParser.DefaultMaxMessageSize + 1) + "</root>";
        var url = CreateRedirectUrl(xml);

        var ex = await Should.ThrowAsync<InvalidOperationException>(() => binding.UnBindAsync(url, NullResolver, _ct));

        ex.Message.ShouldBe("Maximum stream size exceeded.");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task UnbindWithDecompressionBombShouldThrow()
    {
        var binding = CreateBinding();
        var url = CreateDecompressionBombUrl();

        var ex = await Should.ThrowAsync<InvalidOperationException>(() => binding.UnBindAsync(url, NullResolver, _ct));

        ex.Message.ShouldBe("Maximum stream size exceeded.");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task UnbindWithBillionLaughsPayloadShouldThrowXmlException()
    {
        var binding = CreateBinding();
        var xml = """
            <?xml version="1.0"?>
            <!DOCTYPE lolz [
              <!ENTITY lol "lol">
              <!ENTITY lol2 "&lol;&lol;&lol;&lol;&lol;&lol;&lol;&lol;&lol;&lol;">
              <!ENTITY lol3 "&lol2;&lol2;&lol2;&lol2;&lol2;&lol2;&lol2;&lol2;&lol2;&lol2;">
              <!ENTITY lol4 "&lol3;&lol3;&lol3;&lol3;&lol3;&lol3;&lol3;&lol3;&lol3;&lol3;">
              <!ENTITY lol5 "&lol4;&lol4;&lol4;&lol4;&lol4;&lol4;&lol4;&lol4;&lol4;&lol4;">
            ]>
            <root>&lol5;</root>
            """;
        var url = CreateRedirectUrl(xml);

        var ex = await Should.ThrowAsync<XmlException>(() => binding.UnBindAsync(url, NullResolver, _ct));

        ex.Message.ShouldBe(
            "Failed to parse XML document with secure settings. " +
            "The document may contain prohibited constructs (DTD, external entities) or be malformed.");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task UnbindWithDuplicateSignatureParametersThrows()
    {
        var binding = CreateBinding();
        var xml = SimpleAuthnRequestXml();
        var encoded = DeflateAndEncode(xml);
        var url = $"https://idp.example.com/saml?SAMLRequest={encoded}&Signature=a&Signature=b";

        var ex = await Should.ThrowAsync<InvalidOperationException>(() => binding.UnBindAsync(url, NullResolver, _ct));

        ex.Message.ShouldContain("Duplicate Signature");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task UnbindWithDuplicateSigAlgParametersThrows()
    {
        var binding = CreateBinding();
        var xml = SimpleAuthnRequestXml();
        var encoded = DeflateAndEncode(xml);
        var url = $"https://idp.example.com/saml?SAMLRequest={encoded}&SigAlg=a&SigAlg=b";

        var ex = await Should.ThrowAsync<InvalidOperationException>(() => binding.UnBindAsync(url, NullResolver, _ct));

        ex.Message.ShouldContain("Duplicate SigAlg");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task UnbindWithBothSamlRequestAndSamlResponseThrows()
    {
        var binding = CreateBinding();
        var xml = SimpleAuthnRequestXml();
        var encoded = DeflateAndEncode(xml);
        var url = $"https://idp.example.com/saml?SAMLRequest={encoded}&SAMLResponse={encoded}";

        var ex = await Should.ThrowAsync<InvalidOperationException>(() => binding.UnBindAsync(url, NullResolver, _ct));

        ex.Message.ShouldContain("Duplicate message");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task UnbindWithDuplicateRelayStateParametersThrows()
    {
        var binding = CreateBinding();
        var xml = SimpleAuthnRequestXml();
        var encoded = DeflateAndEncode(xml);
        var url = $"https://idp.example.com/saml?SAMLRequest={encoded}&RelayState=a&RelayState=b";

        var ex = await Should.ThrowAsync<InvalidOperationException>(() => binding.UnBindAsync(url, NullResolver, _ct));

        ex.Message.ShouldContain("Duplicate RelayState");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task UnbindWithEmptySignatureDoesNotThrow()
    {
        var binding = CreateBinding();
        var xml = SimpleAuthnRequestXml();
        var encoded = DeflateAndEncode(xml);
        var url = $"https://idp.example.com/saml?SAMLRequest={encoded}&Signature=&SigAlg=";

        // Empty values treated as absent — should not throw
        var result = await binding.UnBindAsync(url, NullResolver, _ct);

        result.Name.ShouldBe("SAMLRequest");
        result.TrustLevel.ShouldBe(TrustLevel.None);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task UnbindFromHttpRequestValidatesSignatureAndSetsTrustLevel()
    {
        using var cert = CreateTestCert();
        var resolver = ResolverFor("https://sp.example.com", cert);

        var binding = CreateBinding();
        var xml = SimpleAuthnRequestXml();
        var (encodedRequest, signature, sigAlgEncoded) = SignRequest(xml, cert);
        var qs = $"SAMLRequest={encodedRequest}&SigAlg={sigAlgEncoded}&Signature={Uri.EscapeDataString(signature)}";
        var request = BuildHttpRequest(qs);

        var result = await binding.UnBindAsync(request, resolver);

        result.TrustLevel.ShouldBe(TrustLevel.ConfiguredKey | TrustLevel.HasSignature);
    }

    [Theory]
    [Trait("Category", Category)]
    [InlineData(SignedXml.XmlDsigRSASHA384Url)]
    [InlineData(SignedXml.XmlDsigRSASHA512Url)]
    public async Task UnbindFromHttpRequestValidatesSignatureWithAlternateAlgorithms(string algorithm)
    {
        using var cert = CreateTestCert();
        var resolver = ResolverFor("https://sp.example.com", cert);

        var binding = CreateBinding();
        var xml = SimpleAuthnRequestXml();
        var (encodedRequest, signature, sigAlgEncoded) = SignRequest(xml, cert, algorithm: algorithm);
        var qs = $"SAMLRequest={encodedRequest}&SigAlg={sigAlgEncoded}&Signature={Uri.EscapeDataString(signature)}";
        var request = BuildHttpRequest(qs);

        var result = await binding.UnBindAsync(request, resolver);

        result.TrustLevel.ShouldBe(TrustLevel.ConfiguredKey | TrustLevel.HasSignature);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task UnbindFromHttpRequestWithInvalidSignatureHasNoneTrustLevel()
    {
        using var signingCert = CreateTestCert();
        using var wrongCert = CreateTestCert();
        var resolver = ResolverFor("https://sp.example.com", signingCert);

        var binding = CreateBinding();
        var xml = SimpleAuthnRequestXml();
        var (encodedRequest, signature, sigAlgEncoded) = SignRequest(xml, wrongCert);
        var qs = $"SAMLRequest={encodedRequest}&SigAlg={sigAlgEncoded}&Signature={Uri.EscapeDataString(signature)}";
        var request = BuildHttpRequest(qs);

        var result = await binding.UnBindAsync(request, resolver);

        result.TrustLevel.ShouldBe(TrustLevel.None);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task UnbindFromHttpRequestWithUnsupportedAlgorithmHasNoneTrustLevel()
    {
        using var cert = CreateTestCert();
        var resolver = ResolverFor("https://sp.example.com", cert);

        var binding = CreateBinding();
        var xml = SimpleAuthnRequestXml();
        var (encodedRequest, signature, _) = SignRequest(xml, cert);
        // Use rsa-sha1 (not in allowlist)
        var sha1AlgEncoded = Uri.EscapeDataString("http://www.w3.org/2000/09/xmldsig#rsa-sha1");
        var qs = $"SAMLRequest={encodedRequest}&SigAlg={sha1AlgEncoded}&Signature={Uri.EscapeDataString(signature)}";
        var request = BuildHttpRequest(qs);

        var result = await binding.UnBindAsync(request, resolver);

        result.TrustLevel.ShouldBe(TrustLevel.None);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task UnbindFromHttpRequestWithInvalidBase64SignatureHasNoneTrustLevel()
    {
        using var cert = CreateTestCert();
        var resolver = ResolverFor("https://sp.example.com", cert);

        var binding = CreateBinding();
        var xml = SimpleAuthnRequestXml();
        var encodedRequest = DeflateAndEncode(xml);
        var sigAlgEncoded = Uri.EscapeDataString(SignedXml.XmlDsigRSASHA256Url);
        var qs = $"SAMLRequest={encodedRequest}&SigAlg={sigAlgEncoded}&Signature=!!!not-valid-base64!!!";
        var request = BuildHttpRequest(qs);

        var result = await binding.UnBindAsync(request, resolver);

        result.TrustLevel.ShouldBe(TrustLevel.None);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task UnbindFromHttpRequestWithoutSignatureHasNoneTrustLevel()
    {
        var binding = CreateBinding();
        var resolver = NullResolver;
        var xml = SimpleAuthnRequestXml();
        var encodedRequest = DeflateAndEncode(xml);
        var qs = $"SAMLRequest={encodedRequest}&RelayState=state123";
        var request = BuildHttpRequest(qs);

        var result = await binding.UnBindAsync(request, resolver);

        result.TrustLevel.ShouldBe(TrustLevel.None);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task UnbindFromHttpRequestWithIncompleteSignatureParamsThrows()
    {
        var binding = CreateBinding();
        var resolver = NullResolver;
        var xml = SimpleAuthnRequestXml();
        var encodedRequest = DeflateAndEncode(xml);
        // Has Signature but no SigAlg
        var qs = $"SAMLRequest={encodedRequest}&Signature=AAAA";
        var request = BuildHttpRequest(qs);

        await Should.ThrowAsync<InvalidOperationException>(() => binding.UnBindAsync(request, resolver));
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task UnbindFromHttpRequestWithSigAlgButNoSignatureThrows()
    {
        var binding = CreateBinding();
        var resolver = NullResolver;
        var xml = SimpleAuthnRequestXml();
        var encodedRequest = DeflateAndEncode(xml);
        var sigAlgEncoded = Uri.EscapeDataString(SignedXml.XmlDsigRSASHA256Url);
        var qs = $"SAMLRequest={encodedRequest}&SigAlg={sigAlgEncoded}";
        var request = BuildHttpRequest(qs);

        await Should.ThrowAsync<InvalidOperationException>(() => binding.UnBindAsync(request, resolver));
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task UnbindFromHttpRequestSucceedsWithMultipleCertificates()
    {
        using var cert1 = CreateTestCert();
        using var cert2 = CreateTestCert();
        using var cert3 = CreateTestCert();
        var resolver = ResolverFor("https://sp.example.com", cert1, cert2, cert3);

        var binding = CreateBinding();
        var xml = SimpleAuthnRequestXml();
        // Sign with cert2 (middle cert)
        var (encodedRequest, signature, sigAlgEncoded) = SignRequest(xml, cert2);
        var qs = $"SAMLRequest={encodedRequest}&SigAlg={sigAlgEncoded}&Signature={Uri.EscapeDataString(signature)}";
        var request = BuildHttpRequest(qs);

        var result = await binding.UnBindAsync(request, resolver);

        result.TrustLevel.ShouldBe(TrustLevel.ConfiguredKey | TrustLevel.HasSignature);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task UnbindFromHttpRequestWithMissingIssuerAndSignatureHasNoneTrustLevel()
    {
        using var cert = CreateTestCert();
        var resolver = ResolverFor("https://sp.example.com", cert);

        var binding = CreateBinding();
        // XML without <Issuer> element
        var xmlNoIssuer = """<samlp:AuthnRequest xmlns:samlp="urn:oasis:names:tc:SAML:2.0:protocol" ID="_test" Version="2.0" IssueInstant="2024-01-01T00:00:00Z"/>""";
        var (encodedRequest, signature, sigAlgEncoded) = SignRequest(xmlNoIssuer, cert);
        var qs = $"SAMLRequest={encodedRequest}&SigAlg={sigAlgEncoded}&Signature={Uri.EscapeDataString(signature)}";
        var request = BuildHttpRequest(qs);

        var result = await binding.UnBindAsync(request, resolver);

        result.TrustLevel.ShouldBe(TrustLevel.None);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task UnbindFromHttpRequestWithIssuerNotFirstChildHasNoneTrustLevel()
    {
        using var cert = CreateTestCert();
        var resolver = ResolverFor("https://sp.example.com", cert);

        var binding = CreateBinding();
        // XML with <Issuer> present but NOT as the first child element
        var xmlIssuerNotFirst = """<samlp:AuthnRequest xmlns:samlp="urn:oasis:names:tc:SAML:2.0:protocol" xmlns:saml="urn:oasis:names:tc:SAML:2.0:assertion" ID="_test" Version="2.0" IssueInstant="2024-01-01T00:00:00Z"><samlp:Extensions/><saml:Issuer>https://sp.example.com</saml:Issuer></samlp:AuthnRequest>""";
        var (encodedRequest, signature, sigAlgEncoded) = SignRequest(xmlIssuerNotFirst, cert);
        var qs = $"SAMLRequest={encodedRequest}&SigAlg={sigAlgEncoded}&Signature={Uri.EscapeDataString(signature)}";
        var request = BuildHttpRequest(qs);

        var result = await binding.UnBindAsync(request, resolver);

        result.TrustLevel.ShouldBe(TrustLevel.None);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task UnbindFromHttpRequestWithUnknownIssuerAndSignatureHasNoneTrustLevel()
    {
        var binding = CreateBinding(); // store returns null for any entityId
        var resolver = NullResolver;
        using var cert = CreateTestCert();
        var xml = SimpleAuthnRequestXml("https://unknown-sp.example.com");
        var (encodedRequest, signature, sigAlgEncoded) = SignRequest(xml, cert);
        var qs = $"SAMLRequest={encodedRequest}&SigAlg={sigAlgEncoded}&Signature={Uri.EscapeDataString(signature)}";
        var request = BuildHttpRequest(qs);

        var result = await binding.UnBindAsync(request, resolver);

        result.TrustLevel.ShouldBe(TrustLevel.None);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task UnbindFromHttpRequestWithEmptySigningCertificatesHasNoneTrustLevel()
    {
        var resolver = ResolverFor("https://sp.example.com");

        var binding = CreateBinding();
        using var cert = CreateTestCert();
        var xml = SimpleAuthnRequestXml();
        var (encodedRequest, signature, sigAlgEncoded) = SignRequest(xml, cert);
        var qs = $"SAMLRequest={encodedRequest}&SigAlg={sigAlgEncoded}&Signature={Uri.EscapeDataString(signature)}";
        var request = BuildHttpRequest(qs);

        var result = await binding.UnBindAsync(request, resolver);

        result.TrustLevel.ShouldBe(TrustLevel.None);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task UnbindFromHttpRequestWithRelayStateValidatesSignatureCorrectly()
    {
        using var cert = CreateTestCert();
        var resolver = ResolverFor("https://sp.example.com", cert);

        var binding = CreateBinding();
        var xml = SimpleAuthnRequestXml();
        var (encodedRequest, signature, sigAlgEncoded) = SignRequest(xml, cert, relayState: "state123");
        var qs = $"SAMLRequest={encodedRequest}&RelayState={Uri.EscapeDataString("state123")}&SigAlg={sigAlgEncoded}&Signature={Uri.EscapeDataString(signature)}";
        var request = BuildHttpRequest(qs);

        var result = await binding.UnBindAsync(request, resolver);

        result.TrustLevel.ShouldBe(TrustLevel.ConfiguredKey | TrustLevel.HasSignature);
        result.RelayState.ShouldBe("state123");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task UnbindFromHttpRequestWithNonCanonicalParameterOrderValidatesSignature()
    {
        using var cert = CreateTestCert();
        var resolver = ResolverFor("https://sp.example.com", cert);

        var binding = CreateBinding();
        var xml = SimpleAuthnRequestXml();
        var (encodedRequest, signature, sigAlgEncoded) = SignRequest(xml, cert, relayState: "relay");
        // Parameters deliberately in non-canonical order
        var qs = $"SigAlg={sigAlgEncoded}&RelayState={Uri.EscapeDataString("relay")}&Signature={Uri.EscapeDataString(signature)}&SAMLRequest={encodedRequest}";
        var request = BuildHttpRequest(qs);

        var result = await binding.UnBindAsync(request, resolver);

        result.TrustLevel.ShouldBe(TrustLevel.ConfiguredKey | TrustLevel.HasSignature);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task UnbindFromHttpRequestWithEncodedRelayStateValidatesSignatureCorrectly()
    {
        using var cert = CreateTestCert();
        var resolver = ResolverFor("https://sp.example.com", cert);

        var binding = CreateBinding();
        var xml = SimpleAuthnRequestXml();
        var relayState = "hello world & more";
        var (encodedRequest, signature, sigAlgEncoded) = SignRequest(xml, cert, relayState: relayState);
        var qs = $"SAMLRequest={encodedRequest}&RelayState={Uri.EscapeDataString(relayState)}&SigAlg={sigAlgEncoded}&Signature={Uri.EscapeDataString(signature)}";
        var request = BuildHttpRequest(qs);

        var result = await binding.UnBindAsync(request, resolver);

        result.TrustLevel.ShouldBe(TrustLevel.ConfiguredKey | TrustLevel.HasSignature);
        result.RelayState.ShouldBe(relayState);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task unbind_from_http_request_with_ecdsa_sha256_signature_has_configured_key_trust_level()
    {
        using var cert = CreateEcdsaTestCert();
        var resolver = ResolverFor("https://sp.example.com", cert);

        var binding = CreateBinding();
        var xml = SimpleAuthnRequestXml();
        var (encodedRequest, signature, sigAlgEncoded) = SignRequestEcdsa(xml, cert, SamlConstants.EcdsaAlgorithms.EcdsaSha256);
        var qs = $"SAMLRequest={encodedRequest}&SigAlg={sigAlgEncoded}&Signature={Uri.EscapeDataString(signature)}";
        var request = BuildHttpRequest(qs);

        var result = await binding.UnBindAsync(request, resolver);

        result.TrustLevel.ShouldBe(TrustLevel.ConfiguredKey | TrustLevel.HasSignature);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task unbind_from_http_request_with_ecdsa_sha384_signature_has_configured_key_trust_level()
    {
        using var cert = CreateEcdsaTestCert(ECCurve.NamedCurves.nistP384);
        var resolver = ResolverFor("https://sp.example.com", cert);

        var binding = CreateBinding();
        var xml = SimpleAuthnRequestXml();
        var (encodedRequest, signature, sigAlgEncoded) = SignRequestEcdsa(xml, cert, SamlConstants.EcdsaAlgorithms.EcdsaSha384);
        var qs = $"SAMLRequest={encodedRequest}&SigAlg={sigAlgEncoded}&Signature={Uri.EscapeDataString(signature)}";
        var request = BuildHttpRequest(qs);

        var result = await binding.UnBindAsync(request, resolver);

        result.TrustLevel.ShouldBe(TrustLevel.ConfiguredKey | TrustLevel.HasSignature);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task unbind_from_http_request_with_ecdsa_sha512_signature_has_configured_key_trust_level()
    {
        using var cert = CreateEcdsaTestCert(ECCurve.NamedCurves.nistP521);
        var resolver = ResolverFor("https://sp.example.com", cert);

        var binding = CreateBinding();
        var xml = SimpleAuthnRequestXml();
        var (encodedRequest, signature, sigAlgEncoded) = SignRequestEcdsa(xml, cert, SamlConstants.EcdsaAlgorithms.EcdsaSha512);
        var qs = $"SAMLRequest={encodedRequest}&SigAlg={sigAlgEncoded}&Signature={Uri.EscapeDataString(signature)}";
        var request = BuildHttpRequest(qs);

        var result = await binding.UnBindAsync(request, resolver);

        result.TrustLevel.ShouldBe(TrustLevel.ConfiguredKey | TrustLevel.HasSignature);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task unbind_from_http_request_with_ecdsa_cert_and_rsa_sig_alg_has_none_trust_level()
    {
        // Cross-algorithm rejection: ECDSA cert presented but RSA sigAlg — must fail
        using var ecdsaCert = CreateEcdsaTestCert();
        var resolver = ResolverFor("https://sp.example.com", ecdsaCert);

        var binding = CreateBinding();
        var xml = SimpleAuthnRequestXml();

        // Sign with ECDSA but claim RSA sigAlg
        var encodedRequest = DeflateAndEncode(xml);
        var sigAlgEncoded = Uri.EscapeDataString(SignedXml.XmlDsigRSASHA256Url);
        var contentToSign = $"SAMLRequest={encodedRequest}&SigAlg={sigAlgEncoded}";
        using var ecdsa = ecdsaCert.GetECDsaPrivateKey()!;
        var sigBytes = ecdsa.SignData(Encoding.UTF8.GetBytes(contentToSign), HashAlgorithmName.SHA256);
        var qs = $"SAMLRequest={encodedRequest}&SigAlg={sigAlgEncoded}&Signature={Uri.EscapeDataString(Convert.ToBase64String(sigBytes))}";
        var request = BuildHttpRequest(qs);

        var result = await binding.UnBindAsync(request, resolver);

        result.TrustLevel.ShouldBe(TrustLevel.None);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task unbind_from_http_request_with_rsa_cert_and_ecdsa_sig_alg_has_none_trust_level()
    {
        // Cross-algorithm rejection: RSA cert presented but ECDSA sigAlg — must fail
        using var rsaCert = CreateTestCert();
        var resolver = ResolverFor("https://sp.example.com", rsaCert);

        var binding = CreateBinding();
        var xml = SimpleAuthnRequestXml();

        // Sign with RSA but claim ECDSA sigAlg
        var encodedRequest = DeflateAndEncode(xml);
        var sigAlgEncoded = Uri.EscapeDataString(SamlConstants.EcdsaAlgorithms.EcdsaSha256);
        var contentToSign = $"SAMLRequest={encodedRequest}&SigAlg={sigAlgEncoded}";
        using var rsa = rsaCert.GetRSAPrivateKey()!;
        var sigBytes = rsa.SignData(Encoding.UTF8.GetBytes(contentToSign), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var qs = $"SAMLRequest={encodedRequest}&SigAlg={sigAlgEncoded}&Signature={Uri.EscapeDataString(Convert.ToBase64String(sigBytes))}";
        var request = BuildHttpRequest(qs);

        var result = await binding.UnBindAsync(request, resolver);

        result.TrustLevel.ShouldBe(TrustLevel.None);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task UnbindRejectsSignatureWhenAlgorithmNotInSpAllowedList()
    {
        using var cert = CreateTestCert();
        // Only allow SHA-384, but sign with SHA-256
        var resolver = ResolverWithAllowedAlgorithms(
            "https://sp.example.com", cert, [SignedXml.XmlDsigRSASHA384Url]);

        var binding = CreateBinding();
        var xml = SimpleAuthnRequestXml();
        var (encodedRequest, signature, sigAlgEncoded) = SignRequest(xml, cert, algorithm: SignedXml.XmlDsigRSASHA256Url);
        var qs = $"SAMLRequest={encodedRequest}&SigAlg={sigAlgEncoded}&Signature={Uri.EscapeDataString(signature)}";
        var request = BuildHttpRequest(qs);

        var result = await binding.UnBindAsync(request, resolver);

        result.TrustLevel.ShouldBe(TrustLevel.None);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task UnbindAcceptsSignatureWhenAlgorithmInSpAllowedList()
    {
        using var cert = CreateTestCert();
        // Explicitly allow SHA-256
        var resolver = ResolverWithAllowedAlgorithms(
            "https://sp.example.com", cert, [SignedXml.XmlDsigRSASHA256Url]);

        var binding = CreateBinding();
        var xml = SimpleAuthnRequestXml();
        var (encodedRequest, signature, sigAlgEncoded) = SignRequest(xml, cert, algorithm: SignedXml.XmlDsigRSASHA256Url);
        var qs = $"SAMLRequest={encodedRequest}&SigAlg={sigAlgEncoded}&Signature={Uri.EscapeDataString(signature)}";
        var request = BuildHttpRequest(qs);

        var result = await binding.UnBindAsync(request, resolver);

        result.TrustLevel.ShouldBe(TrustLevel.ConfiguredKey | TrustLevel.HasSignature);
    }

    private static X509Certificate2 CreateEcdsaTestCert(ECCurve? curve = null)
    {
        using var ecdsa = ECDsa.Create(curve ?? ECCurve.NamedCurves.nistP256);
        var req = new CertificateRequest("CN=Test ECDSA", ecdsa, HashAlgorithmName.SHA256);
        req.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, critical: true));
        var cert = req.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
        return X509CertificateLoader.LoadPkcs12(cert.Export(X509ContentType.Pfx), null, X509KeyStorageFlags.Exportable);
    }

    private static (string encodedRequest, string signature, string sigAlgEncoded) SignRequestEcdsa(
        string xmlPayload,
        X509Certificate2 cert,
        string algorithm)
    {
        var encodedRequest = DeflateAndEncode(xmlPayload);
        var sigAlgEncoded = Uri.EscapeDataString(algorithm);
        var contentToSign = $"SAMLRequest={encodedRequest}&SigAlg={sigAlgEncoded}";

        var hashAlg = algorithm == SamlConstants.EcdsaAlgorithms.EcdsaSha384 ? HashAlgorithmName.SHA384
            : algorithm == SamlConstants.EcdsaAlgorithms.EcdsaSha512 ? HashAlgorithmName.SHA512
            : HashAlgorithmName.SHA256;

        using var ecdsa = cert.GetECDsaPrivateKey()!;
        var sigBytes = ecdsa.SignData(Encoding.UTF8.GetBytes(contentToSign), hashAlg);
        return (encodedRequest, Convert.ToBase64String(sigBytes), sigAlgEncoded);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task GetQueryStringProducesSignatureThatPassesVerification()
    {
        // Arrange: build a signed outbound message using GetQueryString
        using var cert = CreateTestCert();
        var xml = """<samlp:AuthnRequest xmlns:samlp="urn:oasis:names:tc:SAML:2.0:protocol" ID="_test" Version="2.0" IssueInstant="2024-01-01T00:00:00Z"><saml:Issuer xmlns:saml="urn:oasis:names:tc:SAML:2.0:assertion">https://idp.example.com</saml:Issuer></samlp:AuthnRequest>""";
        var doc = new XmlDocument();
        doc.LoadXml(xml);

        var message = new OutboundSaml2Message
        {
            Name = SamlConstants.RequestProperties.SAMLRequest,
            Xml = doc.DocumentElement!,
            Destination = "https://sp.example.com/saml/sso",
            Binding = SamlConstants.Bindings.HttpRedirect,
            SigningCertificate = cert
        };

        // Act: produce the signed query string
        var queryString = HttpRedirectBinding.GetQueryString(message);

        // Assert: unbind the URL and verify the signature is valid
        var url = $"https://sp.example.com/saml/sso{queryString}";
        var resolver = ResolverFor("https://idp.example.com", cert);
        var binding = CreateBinding();

        var result = await binding.UnBindAsync(url, resolver, _ct);

        result.TrustLevel.ShouldBe(TrustLevel.ConfiguredKey | TrustLevel.HasSignature);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task GetQueryStringWithRelayStateProducesSignatureThatPassesVerification()
    {
        using var cert = CreateTestCert();
        var xml = """<samlp:LogoutRequest xmlns:samlp="urn:oasis:names:tc:SAML:2.0:protocol" ID="_test" Version="2.0" IssueInstant="2024-01-01T00:00:00Z"><saml:Issuer xmlns:saml="urn:oasis:names:tc:SAML:2.0:assertion">https://idp.example.com</saml:Issuer></samlp:LogoutRequest>""";
        var doc = new XmlDocument();
        doc.LoadXml(xml);

        var message = new OutboundSaml2Message
        {
            Name = SamlConstants.RequestProperties.SAMLRequest,
            Xml = doc.DocumentElement!,
            Destination = "https://sp.example.com/saml/slo",
            Binding = SamlConstants.Bindings.HttpRedirect,
            SigningCertificate = cert,
            RelayState = "some-relay-state-value"
        };

        var queryString = HttpRedirectBinding.GetQueryString(message);
        var url = $"https://sp.example.com/saml/slo{queryString}";
        var resolver = ResolverFor("https://idp.example.com", cert);
        var binding = CreateBinding();

        var result = await binding.UnBindAsync(url, resolver, _ct);

        result.TrustLevel.ShouldBe(TrustLevel.ConfiguredKey | TrustLevel.HasSignature);
        result.RelayState.ShouldBe("some-relay-state-value");
    }
}
