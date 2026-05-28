#nullable enable
// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Xml;
using Duende.IdentityServer.Saml;
using Duende.IdentityServer.Saml.Models;
using Duende.IdentityServer.Saml.Samlp;
using Duende.IdentityServer.Saml.Serialization;
using Duende.IdentityServer.Saml.Xml;
using UnitTests.Common;
using SamlAttribute = Duende.IdentityServer.Saml.SamlAttribute;

namespace UnitTests.Saml;

public sealed class SamlXmlWriterSigningTests
{
    private const string Category = "SamlXmlWriter Signing";
    private const string DsigNamespace = "http://www.w3.org/2000/09/xmldsig#";

    private readonly SamlXmlWriter _writer = new();
    private readonly X509Certificate2 _cert = TestCert.Load();

    private static Response CreateResponse(int assertionCount = 1)
    {
        var response = new Response
        {
            Issuer = "https://idp.example.com",
            IssueInstant = DateTime.UtcNow,
            Destination = "https://sp.example.com/acs",
            Status = new SamlStatus
            {
                StatusCode = new StatusCode { Value = SamlStatusCodes.Success }
            }
        };

        for (var i = 0; i < assertionCount; i++)
        {
            response.Assertions.Add(new Assertion
            {
                Issuer = "https://idp.example.com",
                IssueInstant = DateTime.UtcNow
            });
        }

        return response;
    }

    private static XmlNamespaceManager CreateNamespaceManager(XmlDocument doc)
    {
        var nsmgr = new XmlNamespaceManager(doc.NameTable);
        nsmgr.AddNamespace("saml", SamlConstants.Namespaces.Assertion);
        nsmgr.AddNamespace("samlp", SamlConstants.Namespaces.Protocol);
        nsmgr.AddNamespace("ds", DsigNamespace);
        return nsmgr;
    }

    private static X509Certificate2 CreateEcdsaCertificate(ECCurve? curve = null)
    {
        using var ecdsa = ECDsa.Create(curve ?? ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest("CN=Test ECDSA IdP", ecdsa, HashAlgorithmName.SHA256);
        var cert = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(365));
        var exported = cert.Export(X509ContentType.Pfx, "test");
        return X509CertificateLoader.LoadPkcs12(exported, "test", X509KeyStorageFlags.Exportable);
    }

    private static Response CreateFullResponse()
    {
        var response = new Response
        {
            Issuer = "https://idp.example.com",
            IssueInstant = DateTime.UtcNow,
            Destination = "https://sp.example.com/acs",
            Status = new SamlStatus
            {
                StatusCode = new StatusCode { Value = SamlStatusCodes.Success }
            }
        };

        response.Assertions.Add(new Assertion
        {
            Issuer = "https://idp.example.com",
            IssueInstant = DateTime.UtcNow,
            Subject = new Subject
            {
                NameId = new NameId { Value = "user@example.com", Format = SamlConstants.NameIdentifierFormats.EmailAddress }
            },
            Conditions = new Conditions
            {
                NotBefore = DateTime.UtcNow.AddMinutes(-5),
                NotOnOrAfter = DateTime.UtcNow.AddMinutes(5),
                AudienceRestrictions = { new AudienceRestriction { Audiences = { "https://sp.example.com" } } }
            },
            AuthnStatement = new AuthnStatement
            {
                AuthnInstant = DateTime.UtcNow,
                SessionIndex = "_session123",
                AuthnContext = new AuthnContext
                {
                    AuthnContextClassRef = SamlConstants.AuthnContextClasses.PasswordProtectedTransport
                }
            },
            Attributes =
            {
                new SamlAttribute
                {
                    Name = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress",
                    NameFormat = "urn:oasis:names:tc:SAML:2.0:attrname-format:uri",
                    Values = { "user@example.com" }
                },
                new SamlAttribute
                {
                    Name = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/role",
                    Values = { "admin", "user" }
                }
            }
        });

        return response;
    }

    private static XmlElement GetAssertionSignatureElement(XmlDocument doc)
    {
        var nsmgr = CreateNamespaceManager(doc);
        var signatureElement = doc.SelectSingleNode("//saml:Assertion/ds:Signature", nsmgr) as XmlElement;
        signatureElement.ShouldNotBeNull();
        return signatureElement;
    }

    private static (string? Error, SigningKey? WorkingKey) VerifyAssertionSignature(
        XmlElement signatureElement, X509Certificate2 cert)
    {
        var keys = new[] { new SigningKey { Certificate = cert } };
        return signatureElement.VerifySignature(keys, SamlConstants.DefaultAllowedAlgorithms);
    }

    [Fact]
    [Trait("Category", Category)]
    public void write_without_cert_produces_unsigned_assertions()
    {
        var response = CreateResponse();

        var doc = _writer.Write(response);

        var nsmgr = CreateNamespaceManager(doc);
        var signatures = doc.SelectNodes("//ds:Signature", nsmgr);
        signatures.ShouldNotBeNull();
        signatures.Count.ShouldBe(0);
    }

    [Fact]
    [Trait("Category", Category)]
    public void write_with_cert_signs_assertion()
    {
        var response = CreateResponse();
        _writer.AssertionSigningCertificate = _cert;

        var doc = _writer.Write(response);

        var nsmgr = CreateNamespaceManager(doc);
        var assertionSignature = doc.SelectSingleNode("//saml:Assertion/ds:Signature", nsmgr);
        assertionSignature.ShouldNotBeNull();
    }

    [Fact]
    [Trait("Category", Category)]
    public void write_with_cert_does_not_sign_response()
    {
        var response = CreateResponse();
        _writer.AssertionSigningCertificate = _cert;

        var doc = _writer.Write(response);

        var nsmgr = CreateNamespaceManager(doc);

        // Signature should be inside Assertion, not directly under Response
        var responseSignature = doc.SelectSingleNode("/samlp:Response/ds:Signature", nsmgr);
        responseSignature.ShouldBeNull();
    }

    [Fact]
    [Trait("Category", Category)]
    public void write_with_cert_signature_placed_after_issuer()
    {
        var response = CreateResponse();
        _writer.AssertionSigningCertificate = _cert;

        var doc = _writer.Write(response);

        var nsmgr = CreateNamespaceManager(doc);
        var assertionElement = doc.SelectSingleNode("//saml:Assertion", nsmgr) as XmlElement;
        assertionElement.ShouldNotBeNull();

        XmlNode? issuerNode = null;
        XmlNode? signatureNode = null;
        var issuerIndex = -1;
        var signatureIndex = -1;
        var index = 0;

        foreach (XmlNode child in assertionElement.ChildNodes)
        {
            if (child.LocalName == "Issuer")
            {
                issuerNode = child;
                issuerIndex = index;
            }
            else if (child.LocalName == "Signature")
            {
                signatureNode = child;
                signatureIndex = index;
            }
            index++;
        }

        issuerNode.ShouldNotBeNull();
        signatureNode.ShouldNotBeNull();
        signatureIndex.ShouldBe(issuerIndex + 1);
    }

    [Fact]
    [Trait("Category", Category)]
    public void write_with_cert_signs_multiple_assertions()
    {
        var response = CreateResponse(assertionCount: 3);
        _writer.AssertionSigningCertificate = _cert;

        var doc = _writer.Write(response);

        var nsmgr = CreateNamespaceManager(doc);
        var assertions = doc.SelectNodes("//saml:Assertion", nsmgr);
        assertions.ShouldNotBeNull();
        assertions.Count.ShouldBe(3);

        var signatures = doc.SelectNodes("//saml:Assertion/ds:Signature", nsmgr);
        signatures.ShouldNotBeNull();
        signatures.Count.ShouldBe(3);
    }

    [Fact]
    [Trait("Category", Category)]
    public void write_with_cert_includes_x509_certificate()
    {
        var response = CreateResponse();
        _writer.AssertionSigningCertificate = _cert;

        var doc = _writer.Write(response);

        var nsmgr = CreateNamespaceManager(doc);
        var x509Cert = doc.SelectSingleNode("//saml:Assertion/ds:Signature/ds:KeyInfo/ds:X509Data/ds:X509Certificate", nsmgr);
        x509Cert.ShouldNotBeNull();
        x509Cert.InnerText.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    [Trait("Category", Category)]
    public void write_with_cert_references_assertion_id()
    {
        var response = CreateResponse();
        var expectedId = response.Assertions[0].Id;
        _writer.AssertionSigningCertificate = _cert;

        var doc = _writer.Write(response);

        var nsmgr = CreateNamespaceManager(doc);
        var reference = doc.SelectSingleNode("//saml:Assertion/ds:Signature/ds:SignedInfo/ds:Reference", nsmgr) as XmlElement;
        reference.ShouldNotBeNull();
        reference.GetAttribute("URI").ShouldBe($"#{expectedId}");
    }

    [Fact]
    [Trait("Category", Category)]
    public void write_with_cert_zero_assertions_produces_no_signatures()
    {
        var response = CreateResponse(assertionCount: 0);
        _writer.AssertionSigningCertificate = _cert;

        var doc = _writer.Write(response);

        var nsmgr = CreateNamespaceManager(doc);
        var signatures = doc.SelectNodes("//ds:Signature", nsmgr);
        signatures.ShouldNotBeNull();
        signatures.Count.ShouldBe(0);
    }

    [Fact]
    [Trait("Category", Category)]
    public void write_with_ecdsa_cert_signs_assertion()
    {
        var response = CreateResponse();
        _writer.AssertionSigningCertificate = CreateEcdsaCertificate();

        var doc = _writer.Write(response);

        var nsmgr = CreateNamespaceManager(doc);
        var assertionSignature = doc.SelectSingleNode("//saml:Assertion/ds:Signature", nsmgr);
        assertionSignature.ShouldNotBeNull();
    }

    [Fact]
    [Trait("Category", Category)]
    public void write_with_ecdsa_cert_uses_ecdsa_sha256_algorithm()
    {
        var response = CreateResponse();
        _writer.AssertionSigningCertificate = CreateEcdsaCertificate();

        var doc = _writer.Write(response);

        var nsmgr = CreateNamespaceManager(doc);
        var signatureMethod = doc.SelectSingleNode(
            "//saml:Assertion/ds:Signature/ds:SignedInfo/ds:SignatureMethod", nsmgr) as XmlElement;
        signatureMethod.ShouldNotBeNull();
        signatureMethod.GetAttribute("Algorithm").ShouldBe(SamlConstants.EcdsaAlgorithms.EcdsaSha256);
    }

    [Fact]
    [Trait("Category", Category)]
    public void write_with_ecdsa_p384_cert_uses_ecdsa_sha384_algorithm()
    {
        var response = CreateResponse();
        _writer.AssertionSigningCertificate = CreateEcdsaCertificate(ECCurve.NamedCurves.nistP384);

        var doc = _writer.Write(response);

        var nsmgr = CreateNamespaceManager(doc);
        var signatureMethod = doc.SelectSingleNode(
            "//saml:Assertion/ds:Signature/ds:SignedInfo/ds:SignatureMethod", nsmgr) as XmlElement;
        signatureMethod.ShouldNotBeNull();
        signatureMethod.GetAttribute("Algorithm").ShouldBe(SamlConstants.EcdsaAlgorithms.EcdsaSha384);
    }

    [Fact]
    [Trait("Category", Category)]
    public void write_with_ecdsa_p521_cert_uses_ecdsa_sha512_algorithm()
    {
        var response = CreateResponse();
        _writer.AssertionSigningCertificate = CreateEcdsaCertificate(ECCurve.NamedCurves.nistP521);

        var doc = _writer.Write(response);

        var nsmgr = CreateNamespaceManager(doc);
        var signatureMethod = doc.SelectSingleNode(
            "//saml:Assertion/ds:Signature/ds:SignedInfo/ds:SignatureMethod", nsmgr) as XmlElement;
        signatureMethod.ShouldNotBeNull();
        signatureMethod.GetAttribute("Algorithm").ShouldBe(SamlConstants.EcdsaAlgorithms.EcdsaSha512);
    }

    [Fact]
    [Trait("Category", Category)]
    public void signed_assertion_with_all_children_has_valid_signature()
    {
        var response = CreateFullResponse();
        _writer.AssertionSigningCertificate = _cert;

        var doc = _writer.Write(response);

        var signatureElement = GetAssertionSignatureElement(doc);
        var (error, workingKey) = VerifyAssertionSignature(signatureElement, _cert);

        error.ShouldBeNull();
        workingKey.ShouldNotBeNull();
    }

    [Fact]
    [Trait("Category", Category)]
    public void signed_assertion_with_attributes_has_valid_signature()
    {
        var response = CreateResponse();
        response.Assertions[0].Attributes.Add(new SamlAttribute
        {
            Name = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress",
            NameFormat = "urn:oasis:names:tc:SAML:2.0:attrname-format:uri",
            Values = { "user@example.com" }
        });
        response.Assertions[0].Attributes.Add(new SamlAttribute
        {
            Name = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name",
            Values = { "Test User" }
        });
        _writer.AssertionSigningCertificate = _cert;

        var doc = _writer.Write(response);

        var signatureElement = GetAssertionSignatureElement(doc);
        var (error, _) = VerifyAssertionSignature(signatureElement, _cert);

        error.ShouldBeNull();
    }

    [Fact]
    [Trait("Category", Category)]
    public void signed_assertion_roundtrips_through_serialization()
    {
        var response = CreateFullResponse();
        _writer.AssertionSigningCertificate = _cert;

        var doc = _writer.Write(response);

        // Simulate what the SP does: serialize to string, parse back, verify
        var xml = doc.OuterXml;
        var reparsed = new XmlDocument();
        reparsed.PreserveWhitespace = true;
        reparsed.LoadXml(xml);

        var signatureElement = GetAssertionSignatureElement(reparsed);
        var (error, _) = VerifyAssertionSignature(signatureElement, _cert);

        error.ShouldBeNull();
    }

    [Fact]
    [Trait("Category", Category)]
    public void signed_assertion_with_ecdsa_has_valid_signature()
    {
        using var ecdsaCert = CreateEcdsaCertificate();
        var response = CreateFullResponse();
        _writer.AssertionSigningCertificate = ecdsaCert;

        var doc = _writer.Write(response);

        var signatureElement = GetAssertionSignatureElement(doc);
        var (error, _) = VerifyAssertionSignature(signatureElement, ecdsaCert);

        error.ShouldBeNull();
    }

    [Fact]
    [Trait("Category", Category)]
    public void signed_assertion_detects_tampering()
    {
        var response = CreateFullResponse();
        _writer.AssertionSigningCertificate = _cert;

        var doc = _writer.Write(response);

        // Tamper with the assertion content
        var nsmgr = CreateNamespaceManager(doc);
        var issuerElement = doc.SelectSingleNode("//saml:Assertion/saml:Issuer", nsmgr) as XmlElement;
        issuerElement.ShouldNotBeNull();
        issuerElement.InnerText = "https://evil.example.com";

        var signatureElement = GetAssertionSignatureElement(doc);
        var (error, workingKey) = VerifyAssertionSignature(signatureElement, _cert);

        workingKey.ShouldBeNull();
    }
}
