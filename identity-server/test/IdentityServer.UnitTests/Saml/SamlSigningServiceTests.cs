// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Duende.IdentityServer.Internal.Saml.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Tokens;
using UnitTests.Common;

namespace UnitTests.Saml;

public class SamlSigningServiceTests
{
    private const string Category = "SAML Signing Service";

    private readonly Ct _ct = TestContext.Current.CancellationToken;

    private readonly MockKeyMaterialService _mockKeyMaterialService = new();
    private readonly SamlSigningService _signingService;

    public SamlSigningServiceTests() =>
        _signingService = new SamlSigningService(
            _mockKeyMaterialService,
            NullLogger<SamlSigningService>.Instance);

    private static X509Certificate2 CreateTestCertificate(bool includePrivateKey = true)
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=Test Signing Cert",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(
                X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment,
                critical: true));

        var cert = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddYears(1));

        var exported = cert.Export(X509ContentType.Pfx, "test");
        var certWithKey = X509CertificateLoader.LoadPkcs12(exported, "test", X509KeyStorageFlags.Exportable);

        if (!includePrivateKey)
        {
            // Export without private key
            var publicOnly = certWithKey.Export(X509ContentType.Cert);
            return X509CertificateLoader.LoadCertificate(publicOnly);
        }

        return certWithKey;
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task get_signing_certificate_async_with_valid_x509_certificate_should_return_certificate()
    {
        // Arrange
        var cert = CreateTestCertificate();
        var credentials = new SigningCredentials(new X509SecurityKey(cert), SecurityAlgorithms.RsaSha256);
        _mockKeyMaterialService.SigningCredentials.Add(credentials);

        // Act
        var result = await _signingService.GetSigningCertificateAsync(_ct);

        // Assert
        result.ShouldNotBeNull();
        result.HasPrivateKey.ShouldBeTrue();
        result.Subject.ShouldContain("CN=Test Signing Cert");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task get_signing_certificate_async_with_non_x509_security_key_should_throw_invalid_operation_exception()
    {
        // Arrange
        var key = new SymmetricSecurityKey(new byte[32]);
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        _mockKeyMaterialService.SigningCredentials.Add(credentials);

        // Act & Assert
        var ex = await Should.ThrowAsync<InvalidOperationException>(
            async () => await _signingService.GetSigningCertificateAsync(_ct));

        ex.Message.ShouldBe("Signing credential must be an X509 certificate with private key.");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task get_signing_certificate_async_with_certificate_without_private_key_should_throw_invalid_operation_exception()
    {
        // Arrange
        var cert = CreateTestCertificate(includePrivateKey: false);
        var credentials = new SigningCredentials(new X509SecurityKey(cert), SecurityAlgorithms.RsaSha256);
        _mockKeyMaterialService.SigningCredentials.Add(credentials);

        // Act & Assert
        var ex = await Should.ThrowAsync<InvalidOperationException>(
            async () => await _signingService.GetSigningCertificateAsync(_ct));

        ex.Message.ShouldBe("Signing certificate must have a private key.");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task get_signing_certificate_async_with_no_signing_credential_should_throw_invalid_operation_exception()
    {
        // Arrange - no credentials added to mock service

        // Act & Assert
        var ex = await Should.ThrowAsync<InvalidOperationException>(
            async () => await _signingService.GetSigningCertificateAsync(_ct));

        ex.Message.ShouldBe("No signing credential available. Configure a signing certificate.");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task get_signing_certificate_base64_async_with_valid_x509_certificate_should_return_base64_string()
    {
        // Arrange
        var cert = CreateTestCertificate();
        var credentials = new SigningCredentials(new X509SecurityKey(cert), SecurityAlgorithms.RsaSha256);
        _mockKeyMaterialService.SigningCredentials.Add(credentials);

        // Act
        var result = await _signingService.GetSigningCertificateBase64Async(_ct);

        // Assert
        result.ShouldNotBeNullOrEmpty();
        // Verify it's valid base64
        var bytes = Convert.FromBase64String(result);
        bytes.ShouldNotBeEmpty();

        // Verify it can be loaded as a certificate
        var loadedCert = X509CertificateLoader.LoadCertificate(bytes);
        loadedCert.Subject.ShouldContain("CN=Test Signing Cert");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task get_signing_certificate_base64_async_with_non_x509_security_key_should_throw_invalid_operation_exception()
    {
        // Arrange
        var key = new SymmetricSecurityKey(new byte[32]);
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        _mockKeyMaterialService.SigningCredentials.Add(credentials);

        // Act & Assert
        var ex = await Should.ThrowAsync<InvalidOperationException>(
            async () => await _signingService.GetSigningCertificateBase64Async(_ct));

        ex.Message.ShouldBe("Signing credential key is not an X509SecurityKey and cannot be used to extract an X509 certificate for SAML metadata.");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task get_signing_certificate_base64_async_with_no_signing_credential_should_throw_invalid_operation_exception()
    {
        // Arrange - no credentials added to mock service

        // Act & Assert
        var ex = await Should.ThrowAsync<InvalidOperationException>(
            async () => await _signingService.GetSigningCertificateBase64Async(_ct));

        ex.Message.ShouldBe("No signing credential available. Configure a signing certificate.");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task get_signing_certificate_base64_async_should_export_public_key_only()
    {
        // Arrange
        var cert = CreateTestCertificate();
        var credentials = new SigningCredentials(new X509SecurityKey(cert), SecurityAlgorithms.RsaSha256);
        _mockKeyMaterialService.SigningCredentials.Add(credentials);

        // Act
        var result = await _signingService.GetSigningCertificateBase64Async(_ct);
        var bytes = Convert.FromBase64String(result);
        var exportedCert = X509CertificateLoader.LoadCertificate(bytes);

        // Assert
        exportedCert.HasPrivateKey.ShouldBeFalse();
    }
}
