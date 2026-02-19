// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Xml;
using System.Xml.Linq;
using Duende.IdentityServer.Internal.Saml;
using Duende.IdentityServer.Internal.Saml.Infrastructure;
using Duende.IdentityServer.Internal.Saml.SingleSignin.Models;
using Duende.IdentityServer.Models;
using SamlStatusCode = Duende.IdentityServer.Saml.Models.SamlStatusCode;

namespace UnitTests.Saml;

public class XmlSignatureHelperTests
{
    private const string Category = "XML Signature Helper";

    private readonly ISamlResultSerializer<SamlResponse> _responseSerializer = new SamlResponse.Serializer();

    private readonly SamlServiceProvider _samlServiceProvider = new SamlServiceProvider
    {
        EntityId = "https://sp.example.com",
        DisplayName = "Test Service Provider",
        AssertionConsumerServiceUrls = [new Uri("https://sp.example.com/saml/acs")]
    };

    private static X509Certificate2 CreateTestCertificate()
    {
        // Create a self-signed certificate for testing
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=Test IdP",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        var cert = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddDays(365));

        // Export and re-import to ensure private key is available
        var exported = cert.Export(X509ContentType.Pfx, "test");
        return X509CertificateLoader.LoadPkcs12(exported, "test", X509KeyStorageFlags.Exportable);
    }

    [Fact]
    [Trait("Category", Category)]
    public void sign_response_valid_response_adds_signature()
    {
        var response = new SamlResponse
        {
            ServiceProvider = _samlServiceProvider,
            IssueInstant = DateTime.UtcNow,
            Issuer = "https://idp.example.com",
            Destination = new Uri("https://sp.example.com/acs"),
            Status = new Status
            {
                StatusCode = SamlStatusCode.Success
            },
        };

        var responseElement = _responseSerializer.Serialize(response);
        var cert = CreateTestCertificate();

        var signedXml = XmlSignatureHelper.SignResponse(responseElement, cert);

        signedXml.ShouldContain("Signature");
        signedXml.ShouldContain("SignatureValue");
        signedXml.ShouldContain("X509Certificate");

        signedXml.ShouldContain("<Response");
        var indexOfResponse = signedXml.IndexOf("<Response", StringComparison.InvariantCulture);
        var indexOfSignature = signedXml.IndexOf("<Signature", StringComparison.InvariantCulture);
        indexOfSignature.ShouldBeGreaterThan(indexOfResponse);
    }

    [Fact]
    [Trait("Category", Category)]
    public void sign_assertion_in_response_valid_response_adds_signature_to_assertion()
    {
        var response = new SamlResponse
        {
            ServiceProvider = _samlServiceProvider,
            IssueInstant = DateTime.UtcNow,
            Issuer = "https://idp.example.com",
            Destination = new Uri("https://sp.example.com/acs"),
            Status = new Status { StatusCode = SamlStatusCode.Success },
            Assertion = new Assertion
            {
                IssueInstant = DateTime.UtcNow,
                Issuer = "https://idp.example.com",
                Subject = new Subject
                {
                    NameId = new NameIdentifier
                    {
                        Value = "user@example.com",
                        Format = SamlConstants.NameIdentifierFormats.EmailAddress
                    }
                }
            }
        };

        var responseElement = _responseSerializer.Serialize(response);
        var cert = CreateTestCertificate();

        var signedXml = XmlSignatureHelper.SignAssertionInResponse(responseElement, cert);

        signedXml.ShouldContain("Signature");

        var xmlDoc = new XmlDocument();
        xmlDoc.LoadXml(signedXml);

        var nsmgr = new XmlNamespaceManager(xmlDoc.NameTable);
        nsmgr.AddNamespace("saml", SamlConstants.Namespaces.Assertion);
        nsmgr.AddNamespace("ds", "http://www.w3.org/2000/09/xmldsig#");

        var signatureInAssertion = xmlDoc.SelectSingleNode("//saml:Assertion/ds:Signature", nsmgr);
        signatureInAssertion.ShouldNotBeNull();
    }

    [Fact]
    [Trait("Category", Category)]
    public void sign_both_valid_response_adds_both_signatures()
    {
        var response = new SamlResponse
        {
            ServiceProvider = _samlServiceProvider,
            IssueInstant = DateTime.UtcNow,
            Issuer = "https://idp.example.com",
            Destination = new Uri("https://sp.example.com/acs"),
            Status = new Status { StatusCode = SamlStatusCode.Success },
            Assertion = new Assertion
            {
                IssueInstant = DateTime.UtcNow,
                Issuer = "https://idp.example.com"
            }
        };

        var responseElement = _responseSerializer.Serialize(response);
        var cert = CreateTestCertificate();

        var signedXml = XmlSignatureHelper.SignBoth(responseElement, cert);

        var xmlDoc = new XmlDocument();
        xmlDoc.LoadXml(signedXml);

        var nsmgr = new XmlNamespaceManager(xmlDoc.NameTable);
        nsmgr.AddNamespace("samlp", SamlConstants.Namespaces.Protocol);
        nsmgr.AddNamespace("saml", SamlConstants.Namespaces.Assertion);
        nsmgr.AddNamespace("ds", "http://www.w3.org/2000/09/xmldsig#");

        // Should have signature in Response
        var responseSignature = xmlDoc.SelectSingleNode("//samlp:Response/ds:Signature", nsmgr);
        responseSignature.ShouldNotBeNull();

        // Should have signature in Assertion
        var assertionSignature = xmlDoc.SelectSingleNode("//saml:Assertion/ds:Signature", nsmgr);
        assertionSignature.ShouldNotBeNull();
    }

    [Fact]
    [Trait("Category", Category)]
    public void sign_response_signature_placed_after_issuer()
    {
        var response = new SamlResponse
        {
            ServiceProvider = _samlServiceProvider,
            IssueInstant = DateTime.UtcNow,
            Issuer = "https://idp.example.com",
            Destination = new Uri("https://sp.example.com/acs"),
            Status = new Status { StatusCode = SamlStatusCode.Success }
        };

        var responseElement = _responseSerializer.Serialize(response);
        var cert = CreateTestCertificate();

        var signedXml = XmlSignatureHelper.SignResponse(responseElement, cert);

        var xmlDoc = new XmlDocument();
        xmlDoc.LoadXml(signedXml);

        var responseXmlElement = xmlDoc.DocumentElement;
        responseXmlElement.ShouldNotBeNull();

        // Find Issuer and Signature elements
        XmlNode? issuerNode = null;
        XmlNode? signatureNode = null;

        foreach (XmlNode child in responseXmlElement.ChildNodes)
        {
            if (child.LocalName == "Issuer")
            {
                issuerNode = child;
            }
            else if (child.LocalName == "Signature")
            {
                signatureNode = child;
            }
        }

        issuerNode.ShouldNotBeNull();
        signatureNode.ShouldNotBeNull();

        // Signature should come after Issuer in document order
        var issuerIndex = 0;
        var signatureIndex = 0;
        var index = 0;
        foreach (XmlNode child in responseXmlElement.ChildNodes)
        {
            if (child == issuerNode)
            {
                issuerIndex = index;
            }

            if (child == signatureNode)
            {
                signatureIndex = index;
            }

            index++;
        }

        signatureIndex.ShouldBeGreaterThan(issuerIndex);
    }

    [Fact]
    [Trait("Category", Category)]
    public void sign_response_uses_rsa_sha256_algorithm()
    {
        var response = new SamlResponse
        {
            ServiceProvider = _samlServiceProvider,
            IssueInstant = DateTime.UtcNow,
            Issuer = "https://idp.example.com",
            Destination = new Uri("https://sp.example.com/acs"),
            Status = new Status { StatusCode = SamlStatusCode.Success }
        };

        var responseElement = _responseSerializer.Serialize(response);
        var cert = CreateTestCertificate();

        var signedXml = XmlSignatureHelper.SignResponse(responseElement, cert);

        signedXml.ShouldContain("http://www.w3.org/2001/04/xmldsig-more#rsa-sha256");
        signedXml.ShouldContain("http://www.w3.org/2001/04/xmlenc#sha256");
    }

    [Fact]
    [Trait("Category", Category)]
    public void sign_response_includes_certificate_in_key_info()
    {
        var response = new SamlResponse
        {
            ServiceProvider = _samlServiceProvider,
            IssueInstant = DateTime.UtcNow,
            Issuer = "https://idp.example.com",
            Destination = new Uri("https://sp.example.com/acs"),
            Status = new Status { StatusCode = SamlStatusCode.Success }
        };

        var responseElement = _responseSerializer.Serialize(response);
        var cert = CreateTestCertificate();

        var signedXml = XmlSignatureHelper.SignResponse(responseElement, cert);

        signedXml.ShouldContain("KeyInfo");
        signedXml.ShouldContain("X509Data");
        signedXml.ShouldContain("X509Certificate");
    }

    [Fact]
    [Trait("Category", Category)]
    public void sign_response_element_without_id_throws_exception()
    {
        var samlp = XNamespace.Get("urn:oasis:names:tc:SAML:2.0:protocol");
        var saml = XNamespace.Get("urn:oasis:names:tc:SAML:2.0:assertion");

        var responseElement = new XElement(samlp + "Response",
            new XAttribute(XNamespace.Xmlns + "samlp", samlp),
            new XAttribute(XNamespace.Xmlns + "saml", saml),
            new XElement(saml + "Issuer", "https://idp.example.com"));

        var cert = CreateTestCertificate();

        Should.Throw<ArgumentException>(() =>
            XmlSignatureHelper.SignResponse(responseElement, cert))
            .Message.ShouldContain("ID attribute");
    }

    [Fact]
    [Trait("Category", Category)]
    public void sign_response_invalid_element_throws_exception()
    {
        var invalidElement = new XElement("SomethingElse",
            new XAttribute("ID", "_test"));
        var cert = CreateTestCertificate();

        Should.Throw<ArgumentException>(() =>
            XmlSignatureHelper.SignResponse(invalidElement, cert))
            .Message.ShouldContain("Response");
    }

    [Fact]
    [Trait("Category", Category)]
    public void sign_response_null_certificate_throws_exception()
    {
        var response = new SamlResponse
        {
            ServiceProvider = _samlServiceProvider,
            IssueInstant = DateTime.UtcNow,
            Issuer = "https://idp.example.com",
            Destination = new Uri("https://sp.example.com/acs"),
            Status = new Status { StatusCode = SamlStatusCode.Success }
        };

        var responseElement = _responseSerializer.Serialize(response);

        Should.Throw<ArgumentNullException>(() =>
            XmlSignatureHelper.SignResponse(responseElement, null!));
    }

    [Fact]
    [Trait("Category", Category)]
    public void sign_assertion_in_response_no_assertion_throws_exception()
    {
        var response = new SamlResponse
        {
            ServiceProvider = _samlServiceProvider,
            IssueInstant = DateTime.UtcNow,
            Issuer = "https://idp.example.com",
            Destination = new Uri("https://sp.example.com/acs"),
            Status = new Status { StatusCode = SamlStatusCode.Success }
            // No Assertion!
        };

        var responseElement = _responseSerializer.Serialize(response);
        var cert = CreateTestCertificate();

        Should.Throw<ArgumentException>(() =>
            XmlSignatureHelper.SignAssertionInResponse(responseElement, cert))
            .Message.ShouldContain("Assertion");
    }
}
