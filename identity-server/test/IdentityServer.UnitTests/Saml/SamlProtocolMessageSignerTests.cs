// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Xml.Linq;
using Duende.IdentityServer.Internal.Saml;
using Duende.IdentityServer.Internal.Saml.Infrastructure;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Saml.Models;
using Microsoft.Extensions.Logging.Abstractions;
using UnitTests.Common;

namespace UnitTests.Saml;

public class SamlProtocolMessageSignerTests
{
    private const string Category = "SAML Protocol Message Signer";

    private readonly SamlServiceProvider _samlServiceProvider = new SamlServiceProvider
    {
        EntityId = "https://sp.example.com",
        DisplayName = "Test Service Provider",
        AssertionConsumerServiceUrls = [new Uri("https://sp.example.com/saml/acs")]
    };

    private static X509Certificate2 CreateTestCertificate()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=Test IdP",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        var cert = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddDays(365));

        var exported = cert.Export(X509ContentType.Pfx, "test");
        return X509CertificateLoader.LoadPkcs12(exported, "test", X509KeyStorageFlags.Exportable);
    }

    private SamlProtocolMessageSigner CreateSigner()
    {
        var cert = CreateTestCertificate();
        var mockSigningService = new MockSamlSigningService(cert);

        return new SamlProtocolMessageSigner(
            mockSigningService,
            NullLogger<SamlProtocolMessageSigner>.Instance);
    }

    private static XElement CreateLogoutResponseElement()
    {
        var protocolNs = XNamespace.Get(SamlConstants.Namespaces.Protocol);
        var assertionNs = XNamespace.Get(SamlConstants.Namespaces.Assertion);

        return new XElement(protocolNs + "LogoutResponse",
            new XAttribute("ID", "_test123"),
            new XAttribute("Version", "2.0"),
            new XAttribute("IssueInstant", "2026-01-29T15:00:00.000Z"),
            new XAttribute("Destination", "https://sp.example.com/slo"),
            new XAttribute("InResponseTo", "_request123"),
            new XElement(assertionNs + "Issuer", "https://idp.example.com"),
            new XElement(protocolNs + "Status",
                new XElement(protocolNs + "StatusCode",
                    new XAttribute("Value", SamlStatusCode.Success.ToString()))));
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task sign_protocol_message_should_add_signature()
    {
        var signer = CreateSigner();
        var logoutResponse = CreateLogoutResponseElement();

        var signedXml = await signer.SignProtocolMessage(logoutResponse, _samlServiceProvider);

        signedXml.ShouldContain("Signature");
        signedXml.ShouldContain("SignatureValue");
        signedXml.ShouldContain("X509Certificate");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task sign_protocol_message_signature_should_be_placed_after_issuer()
    {
        var signer = CreateSigner();
        var logoutResponse = CreateLogoutResponseElement();

        var signedXml = await signer.SignProtocolMessage(logoutResponse, _samlServiceProvider);

        var indexOfIssuer = signedXml.IndexOf("<Issuer", StringComparison.InvariantCulture);
        var indexOfSignature = signedXml.IndexOf("<Signature", StringComparison.InvariantCulture);

        indexOfSignature.ShouldBeGreaterThan(indexOfIssuer);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task sign_protocol_message_should_use_rsa_sha256_algorithm()
    {
        var signer = CreateSigner();
        var logoutResponse = CreateLogoutResponseElement();

        var signedXml = await signer.SignProtocolMessage(logoutResponse, _samlServiceProvider);

        signedXml.ShouldContain("http://www.w3.org/2001/04/xmldsig-more#rsa-sha256");
        signedXml.ShouldContain("http://www.w3.org/2001/04/xmlenc#sha256");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task sign_protocol_message_should_include_certificate_in_key_info()
    {
        var signer = CreateSigner();
        var logoutResponse = CreateLogoutResponseElement();

        var signedXml = await signer.SignProtocolMessage(logoutResponse, _samlServiceProvider);

        signedXml.ShouldContain("KeyInfo");
        signedXml.ShouldContain("X509Data");
        signedXml.ShouldContain("X509Certificate");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task sign_query_string_should_add_signature_and_sig_alg()
    {
        var signer = CreateSigner();
        var queryString = "?SAMLRequest=encodedrequest";

        var signedQueryString = await signer.SignQueryString(queryString);

        signedQueryString.ShouldContain("&SigAlg=");
        signedQueryString.ShouldContain("&Signature=");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task sign_query_string_should_preserve_original_query_string()
    {
        var signer = CreateSigner();
        var queryString = "?SAMLRequest=encodedrequest&RelayState=state123";

        var signedQueryString = await signer.SignQueryString(queryString);

        signedQueryString.ShouldStartWith(queryString);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task sign_query_string_should_include_sig_alg_parameter()
    {
        var signer = CreateSigner();
        var queryString = "?SAMLRequest=encodedrequest";

        var signedQueryString = await signer.SignQueryString(queryString);

        // The SigAlg parameter should be present
        signedQueryString.ShouldContain("&SigAlg=");
        var sigAlgPart = signedQueryString.Split("&SigAlg=")[1].Split("&")[0];
        sigAlgPart.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task sign_query_string_signature_should_be_base64_encoded()
    {
        var signer = CreateSigner();
        var queryString = "?SAMLRequest=encodedrequest";

        var signedQueryString = await signer.SignQueryString(queryString);

        var signaturePart = signedQueryString.Split("&Signature=")[1];
        var decodedSignature = Uri.UnescapeDataString(signaturePart);

        // Should be valid base64
        var bytes = Convert.FromBase64String(decodedSignature);
        bytes.ShouldNotBeEmpty();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task sign_query_string_signature_should_be_url_encoded()
    {
        var signer = CreateSigner();
        var queryString = "?SAMLRequest=encodedrequest";

        var signedQueryString = await signer.SignQueryString(queryString);

        // Base64 can contain + and / which should be URL encoded
        signedQueryString.ShouldNotContain("Signature= "); // No unencoded spaces
        signedQueryString.ShouldContain("Signature="); // But has the parameter
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task sign_query_string_with_relay_state_should_sign_complete_string()
    {
        var signer = CreateSigner();
        var queryString = "?SAMLRequest=encoded&RelayState=mystate";

        var signedQueryString = await signer.SignQueryString(queryString);

        // SigAlg should come after RelayState but before Signature
        var sigAlgIndex = signedQueryString.IndexOf("&SigAlg=", StringComparison.Ordinal);
        var relayStateIndex = signedQueryString.IndexOf("RelayState=", StringComparison.Ordinal);
        var signatureIndex = signedQueryString.IndexOf("&Signature=", StringComparison.Ordinal);

        sigAlgIndex.ShouldBeGreaterThan(relayStateIndex);
        signatureIndex.ShouldBeGreaterThan(sigAlgIndex);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task sign_query_string_should_produce_consistent_signature_for_same_input()
    {
        var signer = CreateSigner();
        var queryString = "?SAMLRequest=encodedrequest";

        var signedQueryString1 = await signer.SignQueryString(queryString);
        var signedQueryString2 = await signer.SignQueryString(queryString);

        // Signatures should be identical for same input with same key
        signedQueryString1.ShouldBe(signedQueryString2);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task sign_query_string_should_produce_different_signature_for_different_input()
    {
        var signer = CreateSigner();
        var queryString1 = "?SAMLRequest=request1";
        var queryString2 = "?SAMLRequest=request2";

        var signedQueryString1 = await signer.SignQueryString(queryString1);
        var signedQueryString2 = await signer.SignQueryString(queryString2);

        // Extract just the signature parts
        var signature1 = signedQueryString1.Split("&Signature=")[1];
        var signature2 = signedQueryString2.Split("&Signature=")[1];

        signature1.ShouldNotBe(signature2);
    }

}
