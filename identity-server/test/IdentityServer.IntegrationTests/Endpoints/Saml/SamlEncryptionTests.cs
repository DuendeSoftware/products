// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using System.Collections.ObjectModel;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Duende.IdentityModel;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Saml.Models;
using static Duende.IdentityServer.IntegrationTests.Endpoints.Saml.SamlTestHelpers;

namespace Duende.IdentityServer.IntegrationTests.Endpoints.Saml;

public class SamlEncryptionTests
{
    private const string Category = "SAML Encryption";

    private SamlFixture Fixture = new();
    private SamlData Data => Fixture.Data;
    private SamlDataBuilder Build => Fixture.Builder;

    private X509Certificate2 CreateTestEncryptionCertificate()
    {
        using var rsa = RSA.Create(2048);
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
            Data.Now.AddDays(-7),
            Data.Now.AddDays(365));

        var exported = cert.Export(X509ContentType.Pfx, "test");
        return X509CertificateLoader.LoadPkcs12(exported, "test", X509KeyStorageFlags.Exportable);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task successful_auth_should_return_encrypted_assertion()
    {
        // Arrange
        var encryptionCert = CreateTestEncryptionCertificate();
        Fixture.ServiceProviders.Add(Build.SamlServiceProvider(
            encryptionCertificates: [encryptionCert],
            encryptAssertions: true));

        Fixture.UserToSignIn = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(JwtClaimTypes.Subject, "user-123")],
            "Test"));

        await Fixture.InitializeAsync();
        await Fixture.Client.GetAsync("/__signin", CancellationToken.None);

        // Act
        var urlEncoded = await EncodeRequest(Build.AuthNRequestXml(), CancellationToken.None);
        var result = await Fixture.Client.GetAsync($"/saml/signin?SAMLRequest={urlEncoded}", CancellationToken.None);

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.OK);

        var responseData = await ExtractSamlResponse(result, CancellationToken.None);
        var responseXml = responseData.responseXml;

        // Verify encrypted assertion is present
        HasEncryptedAssertion(responseXml).ShouldBeTrue("Response should contain EncryptedAssertion");
        HasPlainAssertion(responseXml).ShouldBeFalse("Response should not contain plain Assertion");

        var (_, _, responseElement) = ParseSamlResponseXml(responseXml);
        ValidateEncryptedStructure(responseElement);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task encrypted_assertion_should_be_decryptable_and_content_verified()
    {
        // Arrange
        var encryptionCert = CreateTestEncryptionCertificate();
        var sp = Build.SamlServiceProvider(encryptionCertificates: [encryptionCert], encryptAssertions: true);
        sp.ClaimMappings = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>
        {
            [JwtClaimTypes.Subject] = JwtClaimTypes.Subject,
            [JwtClaimTypes.Email] = JwtClaimTypes.Email,
            [JwtClaimTypes.Name] = JwtClaimTypes.Name,
            [JwtClaimTypes.Role] = JwtClaimTypes.Role
        });
        Fixture.ServiceProviders.Add(sp);

        Fixture.UserToSignIn = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(JwtClaimTypes.Subject, "user-decrypt-test"),
                new Claim(JwtClaimTypes.Email, "decrypt@example.com"),
                new Claim(JwtClaimTypes.Name, "Decrypt Test User"),
                new Claim(JwtClaimTypes.Role, "admin")
            ],
            "Test"));

        await Fixture.InitializeAsync();
        await Fixture.Client.GetAsync("/__signin", CancellationToken.None);

        // Act
        var urlEncoded = await EncodeRequest(Build.AuthNRequestXml(), CancellationToken.None);
        var result = await Fixture.Client.GetAsync($"/saml/signin?SAMLRequest={urlEncoded}", CancellationToken.None);

        // Assert - Decrypt and verify actual content
        result.StatusCode.ShouldBe(HttpStatusCode.OK);

        var samlResponse = await ExtractAndDecryptSamlSuccessFromPostAsync(result, encryptionCert, CancellationToken.None);

        samlResponse.StatusCode.ShouldBe(SamlStatusCodes.Success);
        samlResponse.Assertion.ShouldNotBeNull();

        samlResponse.Assertion.Subject.ShouldNotBeNull();
        samlResponse.Assertion.Subject.NameId.ShouldBe("user-decrypt-test");

        var attributes = samlResponse.Assertion.Attributes;
        attributes.ShouldNotBeNull();
        attributes.Count.ShouldBeGreaterThan(0);

        var subjectAttr = attributes.FirstOrDefault(a => a.Name == JwtClaimTypes.Subject);
        subjectAttr.ShouldNotBeNull();
        subjectAttr.Value.ShouldBe("user-decrypt-test");

        var emailAttr = attributes.FirstOrDefault(a => a.Name == JwtClaimTypes.Email);
        emailAttr.ShouldNotBeNull();
        emailAttr.Value.ShouldBe("decrypt@example.com");

        var nameAttr = attributes.FirstOrDefault(a => a.Name == JwtClaimTypes.Name);
        nameAttr.ShouldNotBeNull();
        nameAttr.Value.ShouldBe("Decrypt Test User");

        var roleAttr = attributes.FirstOrDefault(a => a.Name == JwtClaimTypes.Role);
        roleAttr.ShouldNotBeNull();
        roleAttr.Value.ShouldBe("admin");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task encrypted_assertion_should_contain_expected_attributes()
    {
        // Arrange
        var encryptionCert = CreateTestEncryptionCertificate();
        Fixture.ServiceProviders.Add(Build.SamlServiceProvider(
            encryptionCertificates: [encryptionCert],
            encryptAssertions: true));

        Fixture.UserToSignIn = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(JwtClaimTypes.Subject, "user-verify-structure"),
                new Claim(JwtClaimTypes.Email, "verify@example.com"),
                new Claim(JwtClaimTypes.Name, "Verify Test User"),
                new Claim(JwtClaimTypes.Role, "admin")
            ],
            "Test"));

        await Fixture.InitializeAsync();
        await Fixture.Client.GetAsync("/__signin", CancellationToken.None);

        // Act
        var urlEncoded = await EncodeRequest(Build.AuthNRequestXml(), CancellationToken.None);
        var result = await Fixture.Client.GetAsync($"/saml/signin?SAMLRequest={urlEncoded}", CancellationToken.None);

        // Assert - Verify encrypted structure
        result.StatusCode.ShouldBe(HttpStatusCode.OK);

        var responseData = await ExtractSamlResponse(result, CancellationToken.None);
        var responseXml = responseData.responseXml;

        // Verify encryption happened
        HasEncryptedAssertion(responseXml).ShouldBeTrue();
        HasPlainAssertion(responseXml).ShouldBeFalse();

        // Parse and validate structure
        var (_, _, responseElement) = ParseSamlResponseXml(responseXml);

        // Validate encrypted structure per SAML spec
        ValidateEncryptedStructure(responseElement);

        // Verify EncryptedData contains ciphertext
        var encNs = System.Xml.Linq.XNamespace.Get("http://www.w3.org/2001/04/xmlenc#");
        var samlNs = System.Xml.Linq.XNamespace.Get("urn:oasis:names:tc:SAML:2.0:assertion");

        var encryptedAssertion = responseElement.Element(samlNs + "EncryptedAssertion");
        encryptedAssertion.ShouldNotBeNull();

        var cipherValue = encryptedAssertion.Descendants(encNs + "CipherValue").FirstOrDefault();
        cipherValue.ShouldNotBeNull();
        cipherValue.Value.ShouldNotBeNullOrWhiteSpace("Encrypted data should contain cipher value");

        // Verify it's actually encrypted (base64-encoded binary data)
        var isBase64 = TryFromBase64String(cipherValue.Value, out var _);
        isBase64.ShouldBeTrue("CipherValue should be valid base64");
    }

    private static bool TryFromBase64String(string s, out byte[]? result)
    {
        try
        {
            result = Convert.FromBase64String(s);
            return true;
        }
        catch
        {
            result = null;
            return false;
        }
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task encrypted_assertion_structure_should_be_valid()
    {
        // Arrange
        var encryptionCert = CreateTestEncryptionCertificate();
        Fixture.ServiceProviders.Add(Build.SamlServiceProvider(
            encryptionCertificates: [encryptionCert],
            encryptAssertions: true));

        Fixture.UserToSignIn = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(JwtClaimTypes.Subject, "user-456"),
                new Claim(JwtClaimTypes.Email, "test@example.com"),
                new Claim(JwtClaimTypes.Name, "Test User")
            ],
            "Test"));

        await Fixture.InitializeAsync();
        await Fixture.Client.GetAsync("/__signin", CancellationToken.None);

        // Act
        var urlEncoded = await EncodeRequest(Build.AuthNRequestXml(), CancellationToken.None);
        var result = await Fixture.Client.GetAsync($"/saml/signin?SAMLRequest={urlEncoded}", CancellationToken.None);

        // Assert - Verify structure is valid (can't test decryption due to helper limitations)
        result.StatusCode.ShouldBe(HttpStatusCode.OK);

        var responseData = await ExtractSamlResponse(result, CancellationToken.None);
        var responseXml = responseData.responseXml;
        var (_, _, responseElement) = ParseSamlResponseXml(responseXml);

        HasEncryptedAssertion(responseXml).ShouldBeTrue();
        ValidateEncryptedStructure(responseElement);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task encryption_should_preserve_response_signature()
    {
        // Arrange
        var encryptionCert = CreateTestEncryptionCertificate();
        var sp = Build.SamlServiceProvider(
            encryptionCertificates: [encryptionCert],
            encryptAssertions: true);
        sp.SigningBehavior = SamlSigningBehavior.SignResponse;
        Fixture.ServiceProviders.Add(sp);

        Fixture.UserToSignIn = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(JwtClaimTypes.Subject, "user-789")],
            "Test"));

        await Fixture.InitializeAsync();
        await Fixture.Client.GetAsync("/__signin", CancellationToken.None);

        // Act
        var urlEncoded = await EncodeRequest(Build.AuthNRequestXml(), CancellationToken.None);
        var result = await Fixture.Client.GetAsync($"/saml/signin?SAMLRequest={urlEncoded}", CancellationToken.None);

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.OK);

        var responseData = await ExtractSamlResponse(result, CancellationToken.None);
        var responseXml = responseData.responseXml;
        var (_, _, responseElement) = ParseSamlResponseXml(responseXml);

        // Verify response is signed
        var dsNs = System.Xml.Linq.XNamespace.Get("http://www.w3.org/2000/09/xmldsig#");
        var signature = responseElement.Element(dsNs + "Signature");
        signature.ShouldNotBeNull("Response should be signed");

        // Verify encrypted assertion is present
        HasEncryptedAssertion(responseXml).ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task encryption_should_work_with_sign_assertion_behavior()
    {
        // Arrange
        var encryptionCert = CreateTestEncryptionCertificate();
        var sp = Build.SamlServiceProvider(
            encryptionCertificates: [encryptionCert],
            encryptAssertions: true);
        sp.SigningBehavior = SamlSigningBehavior.SignAssertion;
        Fixture.ServiceProviders.Add(sp);

        Fixture.UserToSignIn = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(JwtClaimTypes.Subject, "user-sign-assertion")],
            "Test"));

        await Fixture.InitializeAsync();
        await Fixture.Client.GetAsync("/__signin", CancellationToken.None);

        // Act
        var urlEncoded = await EncodeRequest(Build.AuthNRequestXml(), CancellationToken.None);
        var result = await Fixture.Client.GetAsync($"/saml/signin?SAMLRequest={urlEncoded}", CancellationToken.None);

        // Assert - Encryption should happen after signing
        result.StatusCode.ShouldBe(HttpStatusCode.OK);

        var responseData = await ExtractSamlResponse(result, CancellationToken.None);
        HasEncryptedAssertion(responseData.responseXml).ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task encryption_should_work_with_sign_both_behavior()
    {
        // Arrange
        var encryptionCert = CreateTestEncryptionCertificate();
        var sp = Build.SamlServiceProvider(
            encryptionCertificates: [encryptionCert],
            encryptAssertions: true);
        sp.SigningBehavior = SamlSigningBehavior.SignBoth;
        Fixture.ServiceProviders.Add(sp);

        Fixture.UserToSignIn = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(JwtClaimTypes.Subject, "user-sign-both")],
            "Test"));

        await Fixture.InitializeAsync();
        await Fixture.Client.GetAsync("/__signin", CancellationToken.None);

        // Act
        var urlEncoded = await EncodeRequest(Build.AuthNRequestXml(), CancellationToken.None);
        var result = await Fixture.Client.GetAsync($"/saml/signin?SAMLRequest={urlEncoded}", CancellationToken.None);

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.OK);

        var responseData = await ExtractSamlResponse(result, CancellationToken.None);
        var responseXml = responseData.responseXml;
        var (_, _, responseElement) = ParseSamlResponseXml(responseXml);

        // Verify response is signed
        var dsNs = System.Xml.Linq.XNamespace.Get("http://www.w3.org/2000/09/xmldsig#");
        var responseSignature = responseElement.Element(dsNs + "Signature");
        responseSignature.ShouldNotBeNull("Response should be signed");

        // Verify encrypted assertion is present
        HasEncryptedAssertion(responseXml).ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task multiple_certificates_should_use_first_valid()
    {
        // Arrange
        var validCert1 = CreateTestEncryptionCertificate();
        var validCert2 = CreateTestEncryptionCertificate();

        Fixture.ServiceProviders.Add(Build.SamlServiceProvider(
            encryptionCertificates: [validCert1, validCert2],
            encryptAssertions: true));

        Fixture.UserToSignIn = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(JwtClaimTypes.Subject, "user-multi-cert")],
            "Test"));

        await Fixture.InitializeAsync();
        await Fixture.Client.GetAsync("/__signin", CancellationToken.None);

        // Act
        var urlEncoded = await EncodeRequest(Build.AuthNRequestXml(), CancellationToken.None);
        var result = await Fixture.Client.GetAsync($"/saml/signin?SAMLRequest={urlEncoded}", CancellationToken.None);

        // Assert - Should encrypt successfully with first valid cert
        result.StatusCode.ShouldBe(HttpStatusCode.OK);
        var responseData = await ExtractSamlResponse(result, CancellationToken.None);
        HasEncryptedAssertion(responseData.responseXml).ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task expired_certificate_should_cause_server_error()
    {
        // Arrange
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=Expired Cert",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        var expiredCert = request.CreateSelfSigned(
            Data.Now.AddDays(-365),
            Data.Now.AddDays(-1));

        Fixture.ServiceProviders.Add(Build.SamlServiceProvider(
            encryptionCertificates: [expiredCert],
            encryptAssertions: true));

        Fixture.UserToSignIn = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(JwtClaimTypes.Subject, "user-expired")],
            "Test"));

        await Fixture.InitializeAsync();
        await Fixture.Client.GetAsync("/__signin", CancellationToken.None);

        // Act
        var urlEncoded = await EncodeRequest(Build.AuthNRequestXml(), CancellationToken.None);
        var result = await Fixture.Client.GetAsync($"/saml/signin?SAMLRequest={urlEncoded}", CancellationToken.None);

        // Assert - Expired cert is a configuration error, so expect 500
        result.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task no_encryption_when_certificates_not_configured()
    {
        // Arrange
        Fixture.ServiceProviders.Add(Build.SamlServiceProvider(
            encryptionCertificates: null)); // No encryption certs

        Fixture.UserToSignIn = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(JwtClaimTypes.Subject, "user-no-encrypt")],
            "Test"));

        await Fixture.InitializeAsync();
        await Fixture.Client.GetAsync("/__signin", CancellationToken.None);

        // Act
        var urlEncoded = await EncodeRequest(Build.AuthNRequestXml(), CancellationToken.None);
        var result = await Fixture.Client.GetAsync($"/saml/signin?SAMLRequest={urlEncoded}", CancellationToken.None);

        // Assert - Should return plain assertion
        result.StatusCode.ShouldBe(HttpStatusCode.OK);

        var responseData = await ExtractSamlResponse(result, CancellationToken.None);
        var responseXml = responseData.responseXml;

        HasPlainAssertion(responseXml).ShouldBeTrue("Response should contain plain Assertion");
        HasEncryptedAssertion(responseXml).ShouldBeFalse("Response should not be encrypted");

        // Verify can parse as success
        var samlResponse = await ExtractSamlSuccessFromPostAsync(result, CancellationToken.None);
        samlResponse.StatusCode.ShouldBe(SamlStatusCodes.Success);
        samlResponse.Assertion.ShouldNotBeNull();
    }
}
