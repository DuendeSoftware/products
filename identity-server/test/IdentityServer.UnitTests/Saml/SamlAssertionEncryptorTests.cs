// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Xml.Linq;
using Duende.IdentityServer.Internal.Saml.Infrastructure;
using Duende.IdentityServer.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace UnitTests.Saml;

public class SamlAssertionEncryptorTests
{
    private const string Category = "SAML Assertion Encryptor";

    private static readonly XNamespace SamlNs = "urn:oasis:names:tc:SAML:2.0:assertion";
    private static readonly XNamespace SamlpNs = "urn:oasis:names:tc:SAML:2.0:protocol";
    private static readonly XNamespace EncNs = "http://www.w3.org/2001/04/xmlenc#";

    private readonly SamlAssertionEncryptor _encryptor = new(new FakeTimeProvider(DateTimeOffset.UtcNow), NullLogger<SamlAssertionEncryptor>.Instance);

    private static X509Certificate2 CreateTestEncryptionCertificate(DateTimeOffset? notBefore = null, DateTimeOffset? notAfter = null, int? keySize = 2048)
    {
        using var rsa = RSA.Create(keySize!.Value);
        var request = new CertificateRequest(
            "CN=Test SP Encryption",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(
                X509KeyUsageFlags.KeyEncipherment | X509KeyUsageFlags.DataEncipherment,
                critical: true));

        var cert = request.CreateSelfSigned(
             notBefore ?? DateTimeOffset.UtcNow.AddDays(-1),
            notAfter ?? DateTimeOffset.UtcNow.AddDays(365));

        var exported = cert.Export(X509ContentType.Pfx, "test");
        return X509CertificateLoader.LoadPkcs12(exported, "test", X509KeyStorageFlags.Exportable);
    }

    private static XElement CreateTestResponse() => new(SamlpNs + "Response",
            new XAttribute("ID", "_" + Guid.NewGuid().ToString("N")),
            new XAttribute("Version", "2.0"),
            new XAttribute("IssueInstant", DateTime.UtcNow.ToString("o")),
            new XElement(SamlNs + "Issuer", "https://idp.example.com"),
            new XElement(SamlpNs + "Status",
                new XElement(SamlpNs + "StatusCode",
                    new XAttribute("Value", "urn:oasis:names:tc:SAML:2.0:status:Success"))),
            new XElement(SamlNs + "Assertion",
                new XAttribute("ID", "_" + Guid.NewGuid().ToString("N")),
                new XAttribute("Version", "2.0"),
                new XAttribute("IssueInstant", DateTime.UtcNow.ToString("o")),
                new XElement(SamlNs + "Issuer", "https://idp.example.com"),
                new XElement(SamlNs + "Subject",
                    new XElement(SamlNs + "NameID", "user@example.com")),
                new XElement(SamlNs + "AttributeStatement",
                    new XElement(SamlNs + "Attribute",
                        new XAttribute("Name", "email"),
                        new XElement(SamlNs + "AttributeValue", "user@example.com")))));

    [Fact]
    [Trait("Category", Category)]
    public void no_encryption_certificates_configured_for_service_provider_should_throw()
    {
        // Arrange
        var response = CreateTestResponse();
        var sp = new SamlServiceProvider
        {
            EntityId = "https://sp.example.com",
            DisplayName = "Test Service Provider",
            AssertionConsumerServiceUrls = [new Uri("https://sp.example.com/acs")]
            // No EncryptionCertificates configured
        };

        var originalXml = response.ToString(SaveOptions.DisableFormatting);

        // Act & Assert
        var result = Should.Throw<InvalidOperationException>(() => _encryptor.EncryptAssertion(originalXml, sp));

        result.Message.ShouldBe($"No valid encryption certificate found for {sp.EntityId}. Certificates may be expired, not yet valid, or lacking required RSA keys.");
    }

    [Fact]
    [Trait("Category", Category)]
    public void valid_certificate_should_encrypt_assertion()
    {
        // Arrange
        var response = CreateTestResponse();
        var cert = CreateTestEncryptionCertificate();
        var sp = new SamlServiceProvider
        {
            EntityId = "https://sp.example.com",
            DisplayName = "Test Service Provider",
            AssertionConsumerServiceUrls = [new Uri("https://sp.example.com/acs")],
            EncryptionCertificates = [cert]
        };

        var originalXml = response.ToString(SaveOptions.DisableFormatting);

        // Act
        var resultXml = _encryptor.EncryptAssertion(originalXml, sp);

        // Assert
        resultXml.ShouldNotBeNull();

        var result = XElement.Parse(resultXml);

        // Verify plain assertion removed
        var plainAssertion = result.Element(SamlNs + "Assertion");
        plainAssertion.ShouldBeNull("Plain assertion should be removed after encryption");

        // Verify encrypted assertion added
        var encryptedAssertion = result.Element(SamlNs + "EncryptedAssertion");
        encryptedAssertion.ShouldNotBeNull("Encrypted assertion should be present");

        // Verify structure (EncryptedKey is inside KeyInfo)
        var encryptedData = encryptedAssertion.Element(EncNs + "EncryptedData");
        encryptedData.ShouldNotBeNull();

        var dsNs = XNamespace.Get("http://www.w3.org/2000/09/xmldsig#");
        var keyInfo = encryptedData.Element(dsNs + "KeyInfo");
        keyInfo.ShouldNotBeNull();

        var encryptedKey = keyInfo.Element(EncNs + "EncryptedKey");
        encryptedKey.ShouldNotBeNull();
    }

    [Fact]
    [Trait("Category", Category)]
    public void expired_certificate_should_throw_exception()
    {
        // Arrange
        var response = CreateTestResponse();
        var expiredCert = CreateTestEncryptionCertificate(DateTimeOffset.UtcNow.AddDays(-365), DateTimeOffset.UtcNow.AddDays(-1));
        var sp = new SamlServiceProvider
        {
            EntityId = "https://sp.example.com",
            DisplayName = "Test Service Provider",
            AssertionConsumerServiceUrls = [new Uri("https://sp.example.com/acs")],
            EncryptionCertificates = [expiredCert]
        };

        var responseXml = response.ToString(SaveOptions.DisableFormatting);

        // Act & Assert
        var exception = Should.Throw<InvalidOperationException>(() => _encryptor.EncryptAssertion(responseXml, sp));

        exception.Message.ShouldBe($"No valid encryption certificate found for {sp.EntityId}. Certificates may be expired, not yet valid, or lacking required RSA keys.");
    }

    [Fact]
    [Trait("Category", Category)]
    public void not_yet_valid_certificate_should_throw_exception()
    {
        // Arrange
        var response = CreateTestResponse();
        var notYetValidCert = CreateTestEncryptionCertificate(DateTimeOffset.UtcNow.AddDays(1), DateTimeOffset.UtcNow.AddDays(5));
        var sp = new SamlServiceProvider
        {
            EntityId = "https://sp.example.com",
            DisplayName = "Test Service Provider",
            AssertionConsumerServiceUrls = [new Uri("https://sp.example.com/acs")],
            EncryptionCertificates = [notYetValidCert]
        };

        var responseXml = response.ToString(SaveOptions.DisableFormatting);

        // Act & Assert
        var exception = Should.Throw<InvalidOperationException>(() => _encryptor.EncryptAssertion(responseXml, sp));

        exception.Message.ShouldBe($"No valid encryption certificate found for {sp.EntityId}. Certificates may be expired, not yet valid, or lacking required RSA keys.");
    }

    [Fact]
    [Trait("Category", Category)]
    public void no_rsa_public_key_should_throw_exception()
    {
        // Arrange
        var response = CreateTestResponse();

        // Create certificate without RSA key (EC key instead)
        using var ecdsa = ECDsa.Create();
        var request = new CertificateRequest(new X500DistinguishedName("CN=Test EC Certificate"), ecdsa, HashAlgorithmName.SHA256);

        var cert = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddDays(365));

        var sp = new SamlServiceProvider
        {
            EntityId = "https://sp.example.com",
            DisplayName = "Test Service Provider",
            AssertionConsumerServiceUrls = [new Uri("https://sp.example.com/acs")],
            EncryptionCertificates = [cert]
        };

        var responseXml = response.ToString(SaveOptions.DisableFormatting);

        // Act & Assert
        var exception = Should.Throw<InvalidOperationException>(() =>
            _encryptor.EncryptAssertion(responseXml, sp));

        exception.Message.ShouldBe($"No valid encryption certificate found for {sp.EntityId}. Certificates may be expired, not yet valid, or lacking required RSA keys.");
    }

    [Fact]
    [Trait("Category", Category)]
    public void insufficient_key_size_in_certificate_should_throw_exception()
    {
        // Arrange
        var response = CreateTestResponse();

        // Create certificate with RSA key with too small of key size
        var certWithInsufficientKeySizeInCertificate = CreateTestEncryptionCertificate(keySize: 1024);
        var sp = new SamlServiceProvider
        {
            EntityId = "https://sp.example.com",
            DisplayName = "Test Service Provider",
            AssertionConsumerServiceUrls = [new Uri("https://sp.example.com/acs")],
            EncryptionCertificates = [certWithInsufficientKeySizeInCertificate]
        };

        var responseXml = response.ToString(SaveOptions.DisableFormatting);

        // Act & Assert
        var exception = Should.Throw<InvalidOperationException>(() =>
            _encryptor.EncryptAssertion(responseXml, sp));

        exception.Message.ShouldBe($"No valid encryption certificate found for {sp.EntityId}. Certificates may be expired, not yet valid, or lacking required RSA keys.");
    }

    [Fact]
    [Trait("Category", Category)]
    public void no_assertion_should_throw_exception()
    {
        // Arrange - Response without assertion
        var response = new XElement(SamlpNs + "Response",
            new XAttribute("ID", "_" + Guid.NewGuid().ToString("N")),
            new XAttribute("Version", "2.0"),
            new XAttribute("IssueInstant", DateTime.UtcNow.ToString("o")),
            new XElement(SamlNs + "Issuer", "https://idp.example.com"),
            new XElement(SamlpNs + "Status",
                new XElement(SamlpNs + "StatusCode",
                    new XAttribute("Value", "urn:oasis:names:tc:SAML:2.0:status:Success"))));
        // No Assertion element

        var cert = CreateTestEncryptionCertificate();
        var sp = new SamlServiceProvider
        {
            EntityId = "https://sp.example.com",
            DisplayName = "Test Service Provider",
            AssertionConsumerServiceUrls = [new Uri("https://sp.example.com/acs")],
            EncryptionCertificates = [cert]
        };

        var responseXml = response.ToString(SaveOptions.DisableFormatting);

        // Act & Assert
        var exception = Should.Throw<InvalidOperationException>(() => _encryptor.EncryptAssertion(responseXml, sp));

        exception.Message.ShouldBe($"SAML Response does not contain an Assertion element for {sp.EntityId}");
    }

    [Fact]
    [Trait("Category", Category)]
    public void valid_input_should_replace_assertion()
    {
        // Arrange
        var response = CreateTestResponse();
        var cert = CreateTestEncryptionCertificate();
        var sp = new SamlServiceProvider
        {
            EntityId = "https://sp.example.com",
            DisplayName = "Test Service Provider",
            AssertionConsumerServiceUrls = [new Uri("https://sp.example.com/acs")],
            EncryptionCertificates = [cert]
        };

        // Capture original response structure
        var originalResponseId = response.Attribute("ID")?.Value;
        var originalIssuer = response.Element(SamlNs + "Issuer")?.Value;
        var responseXml = response.ToString(SaveOptions.DisableFormatting);

        // Act
        var resultXml = _encryptor.EncryptAssertion(responseXml, sp);

        // Assert - Response structure preserved
        var result = XElement.Parse(resultXml);
        result.Name.ShouldBe(SamlpNs + "Response");
        result.Attribute("ID")?.Value.ShouldBe(originalResponseId);
        result.Element(SamlNs + "Issuer")?.Value.ShouldBe(originalIssuer);

        var status = result.Element(SamlpNs + "Status");
        status.ShouldNotBeNull();
        status.Element(SamlpNs + "StatusCode")?.Attribute("Value")?.Value
            .ShouldBe("urn:oasis:names:tc:SAML:2.0:status:Success");

        // Assert - Only assertion changed
        var children = result.Elements().ToList();
        children.Count.ShouldBe(3); // Issuer, Status, EncryptedAssertion (was Assertion)

        // Issuer should be first
        children[0].Name.ShouldBe(SamlNs + "Issuer");

        // Status should be second
        children[1].Name.ShouldBe(SamlpNs + "Status");

        // EncryptedAssertion should be third (replaced Assertion)
        children[2].Name.ShouldBe(SamlNs + "EncryptedAssertion");
    }
}
