// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Saml.Infrastructure;
using Duende.IdentityServer.Services.KeyManagement;
using Microsoft.IdentityModel.Tokens;
using UnitTests.Common;
using UnitTests.Validation.Setup;

namespace UnitTests.Saml;

public sealed class SamlSigningServiceTests : IDisposable
{
    private const string Category = "SAML Signing Service";

    private readonly Ct _ct = TestContext.Current.CancellationToken;

    private readonly MockKeyMaterialService _mockKeyMaterialService = new();
    private readonly TestIssuerNameService _issuerNameService = new("https://test.identityserver.io");
    private readonly KeyManagementOptions _keyManagementOptions = new();
    private readonly RsaCertificateFactory _rsaCertificateFactory;
    private readonly MockKeyManager _mockKeyManager = new();

    private readonly List<IDisposable> _disposables = [];

    public SamlSigningServiceTests() =>
        _rsaCertificateFactory = new RsaCertificateFactory(_keyManagementOptions);

    public void Dispose()
    {
        foreach (var d in _disposables)
        {
            d.Dispose();
        }
    }

    private SamlSigningService CreateService() =>
        new(_mockKeyMaterialService, _issuerNameService, _mockKeyManager, _rsaCertificateFactory);

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

        using var cert = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddYears(1));

        var exported = cert.Export(X509ContentType.Pfx, "test");
        var certWithKey = X509CertificateLoader.LoadPkcs12(exported, "test", X509KeyStorageFlags.Exportable);

        if (!includePrivateKey)
        {
            var publicOnly = certWithKey.Export(X509ContentType.Cert);
            certWithKey.Dispose();
            return X509CertificateLoader.LoadCertificate(publicOnly);
        }

        return certWithKey;
    }

    private RsaSecurityKey CreateRsaSecurityKeyFromParameters(string keyId = "test-key-id")
    {
        using var rsa = RSA.Create(2048);
        var parameters = rsa.ExportParameters(includePrivateParameters: true);
        var key = new RsaSecurityKey(parameters) { KeyId = keyId };
        _mockKeyManager.Keys.Add(new RsaKeyContainer(key, SecurityAlgorithms.RsaSha256, DateTime.UtcNow));
        return key;
    }

    private RsaSecurityKey CreateRsaSecurityKeyWithRsaObject(string keyId = "test-key-id")
    {
        var rsa = RSA.Create(2048);
        _disposables.Add(rsa);
        var key = new RsaSecurityKey(rsa) { KeyId = keyId };
        _mockKeyManager.Keys.Add(new RsaKeyContainer(key, SecurityAlgorithms.RsaSha256, DateTime.UtcNow));
        return key;
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task GetSigningCertificateAsync_WithValidX509Certificate_ReturnsCertificate()
    {
        using var cert = CreateTestCertificate();
        var credentials = new SigningCredentials(new X509SecurityKey(cert), SecurityAlgorithms.RsaSha256);
        _mockKeyMaterialService.SigningCredentials.Add(credentials);

        var result = await CreateService().GetSigningCertificateAsync(_ct);

        result.ShouldNotBeNull();
        result.HasPrivateKey.ShouldBeTrue();
        result.Subject.ShouldContain("CN=Test Signing Cert");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task GetSigningCertificateAsync_WithCertificateWithoutPrivateKey_ThrowsInvalidOperationException()
    {
        using var cert = CreateTestCertificate(includePrivateKey: false);
        var credentials = new SigningCredentials(new X509SecurityKey(cert), SecurityAlgorithms.RsaSha256);
        _mockKeyMaterialService.SigningCredentials.Add(credentials);

        var ex = await Should.ThrowAsync<InvalidOperationException>(
            async () => await CreateService().GetSigningCertificateAsync(_ct));

        ex.Message.ShouldBe("Signing certificate must have a private key.");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task GetSigningCertificateAsync_WithNoSigningCredential_ThrowsInvalidOperationException()
    {
        var ex = await Should.ThrowAsync<InvalidOperationException>(
            async () => await CreateService().GetSigningCertificateAsync(_ct));

        ex.Message.ShouldBe("No signing credential available. Configure a signing certificate.");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task GetSigningCertificateAsync_WithUnsupportedKeyType_ThrowsInvalidOperationException()
    {
        var key = new SymmetricSecurityKey(new byte[32]);
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        _mockKeyMaterialService.SigningCredentials.Add(credentials);

        var ex = await Should.ThrowAsync<InvalidOperationException>(
            async () => await CreateService().GetSigningCertificateAsync(_ct));

        ex.Message.ShouldBe("Signing credential must be an X509 certificate or RSA key with private key.");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task GetSigningCertificateAsync_WithRsaSecurityKeyFromParameters_ReturnsCertificate()
    {
        var rsaKey = CreateRsaSecurityKeyFromParameters("my-key-id");
        var credentials = new SigningCredentials(rsaKey, SecurityAlgorithms.RsaSha256);
        _mockKeyMaterialService.SigningCredentials.Add(credentials);

        var result = await CreateService().GetSigningCertificateAsync(_ct);

        result.ShouldNotBeNull();
        result.HasPrivateKey.ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task GetSigningCertificateAsync_WithRsaSecurityKeyFromRsaObject_ReturnsCertificate()
    {
        var rsaKey = CreateRsaSecurityKeyWithRsaObject("my-key-id");
        var credentials = new SigningCredentials(rsaKey, SecurityAlgorithms.RsaSha256);
        _mockKeyMaterialService.SigningCredentials.Add(credentials);

        var result = await CreateService().GetSigningCertificateAsync(_ct);

        result.ShouldNotBeNull();
        result.HasPrivateKey.ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task GetSigningCertificateAsync_WithRsaKeyWithNoKeyId_ThrowsInvalidOperationException()
    {
        using var rsa = RSA.Create(2048);
        var parameters = rsa.ExportParameters(includePrivateParameters: true);
        var rsaKey = new RsaSecurityKey(parameters); // No KeyId set
        var credentials = new SigningCredentials(rsaKey, SecurityAlgorithms.RsaSha256);
        _mockKeyMaterialService.SigningCredentials.Add(credentials);

        var ex = await Should.ThrowAsync<InvalidOperationException>(
            async () => await CreateService().GetSigningCertificateAsync(_ct));

        ex.Message.ShouldContain("signing key has no KeyId");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task GetSigningCertificateAsync_WithSameRsaKey_ProducesSameCertificateBytes()
    {
        var rsaKey = CreateRsaSecurityKeyFromParameters("deterministic-key");
        var credentials = new SigningCredentials(rsaKey, SecurityAlgorithms.RsaSha256);
        _mockKeyMaterialService.SigningCredentials.Add(credentials);

        var service = CreateService();
        var cert1 = await service.GetSigningCertificateAsync(_ct);
        var cert2 = await service.GetSigningCertificateAsync(_ct);

        cert1.RawData.ShouldBe(cert2.RawData);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task GetSigningCertificateAsync_WithSameRsaKey_ReturnsCachedInstance()
    {
        var rsaKey = CreateRsaSecurityKeyFromParameters("cached-key");
        var credentials = new SigningCredentials(rsaKey, SecurityAlgorithms.RsaSha256);
        _mockKeyMaterialService.SigningCredentials.Add(credentials);

        var service = CreateService();
        var cert1 = await service.GetSigningCertificateAsync(_ct);
        var cert2 = await service.GetSigningCertificateAsync(_ct);

        ReferenceEquals(cert1, cert2).ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task GetSigningCertificateAsync_WithRsaKey_SerialIsAlwaysPositive()
    {
        // Use a single RSA key and vary only the keyId to test serial derivation.
        // Serial derivation is a pure function of keyId — no need to generate many RSA keys.
        var rsaKey = CreateRsaSecurityKeyFromParameters("base-key");

        for (var i = 0; i < 100; i++)
        {
            _mockKeyMaterialService.SigningCredentials.Clear();
            var keyWithId = new RsaSecurityKey(rsaKey.Parameters) { KeyId = $"key-{i}" };
            _mockKeyManager.Keys.Add(new RsaKeyContainer(keyWithId, SecurityAlgorithms.RsaSha256, DateTime.UtcNow));
            var credentials = new SigningCredentials(keyWithId, SecurityAlgorithms.RsaSha256);
            _mockKeyMaterialService.SigningCredentials.Add(credentials);

            var cert = await CreateService().GetSigningCertificateAsync(_ct);
            var serial = cert.GetSerialNumber(); // little-endian on .NET
            // High byte (last in little-endian) must have bit 7 clear
            (serial[^1] & 0x80).ShouldBe(0);
        }
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task GetSigningCertificateAsync_WithRsaKey_SerialIsStableAcrossCalls()
    {
        var rsaKey = CreateRsaSecurityKeyFromParameters("stable-serial-key");
        var credentials = new SigningCredentials(rsaKey, SecurityAlgorithms.RsaSha256);
        _mockKeyMaterialService.SigningCredentials.Add(credentials);

        var service = CreateService();
        var cert1 = await service.GetSigningCertificateAsync(_ct);
        var cert2 = await service.GetSigningCertificateAsync(_ct);

        cert1.SerialNumber.ShouldBe(cert2.SerialNumber);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task GetSigningCertificateAsync_WithRsaKey_GeneratedCertCanSignAndVerify()
    {
        var rsaKey = CreateRsaSecurityKeyFromParameters("signing-key");
        var credentials = new SigningCredentials(rsaKey, SecurityAlgorithms.RsaSha256);
        _mockKeyMaterialService.SigningCredentials.Add(credentials);

        var cert = await CreateService().GetSigningCertificateAsync(_ct);

        cert.HasPrivateKey.ShouldBeTrue();

        var data = Encoding.UTF8.GetBytes("test data to sign");
        using var rsaForSigning = cert.GetRSAPrivateKey()!;
        using var rsaForVerifying = cert.GetRSAPublicKey()!;

        var signature = rsaForSigning.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var valid = rsaForVerifying.VerifyData(data, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        valid.ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task GetSigningCertificateAsync_WithRsaKey_UseKeyCreatedTimeFromKeyManager()
    {
        var keyCreated = new DateTime(2025, 3, 15, 10, 0, 0, DateTimeKind.Utc);
        using var rsa = RSA.Create(2048);
        var parameters = rsa.ExportParameters(includePrivateParameters: true);
        var rsaKey = new RsaSecurityKey(parameters) { KeyId = "managed-key" };
        var credentials = new SigningCredentials(rsaKey, SecurityAlgorithms.RsaSha256);
        _mockKeyMaterialService.SigningCredentials.Add(credentials);

        _mockKeyManager.Keys.Add(new RsaKeyContainer(rsaKey, SecurityAlgorithms.RsaSha256, keyCreated));

        var cert = await CreateService().GetSigningCertificateAsync(_ct);

        cert.NotBefore.ToUniversalTime().ShouldBe(keyCreated);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task GetSigningCertificateAsync_WithRsaKey_WhenKeyNotInKeyManager_ThrowsInvalidOperationException()
    {
        using var rsa = RSA.Create(2048);
        var parameters = rsa.ExportParameters(includePrivateParameters: true);
        var rsaKey = new RsaSecurityKey(parameters) { KeyId = "unmanaged-key" };
        var credentials = new SigningCredentials(rsaKey, SecurityAlgorithms.RsaSha256);
        _mockKeyMaterialService.SigningCredentials.Add(credentials);

        // Key manager has no keys — simulates a manually registered RSA key
        var ex = await Should.ThrowAsync<InvalidOperationException>(
            async () => await CreateService().GetSigningCertificateAsync(_ct));

        ex.Message.ShouldBe("Cannot auto-wrap a manually registered RSA key as an X509 certificate for SAML signing. Use an X509 certificate directly or enable automatic key management.");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task GetSigningCertificateAsync_WithRsaKey_CertExpiresAtKeyRetirementAge()
    {
        var keyCreated = new DateTime(2025, 3, 15, 10, 0, 0, DateTimeKind.Utc);
        using var rsa = RSA.Create(2048);
        var parameters = rsa.ExportParameters(includePrivateParameters: true);
        var rsaKey = new RsaSecurityKey(parameters) { KeyId = "lifetime-key" };
        var credentials = new SigningCredentials(rsaKey, SecurityAlgorithms.RsaSha256);
        _mockKeyMaterialService.SigningCredentials.Add(credentials);

        _mockKeyManager.Keys.Add(new RsaKeyContainer(rsaKey, SecurityAlgorithms.RsaSha256, keyCreated));

        var cert = await CreateService().GetSigningCertificateAsync(_ct);

        var expectedExpiry = keyCreated.Add(_keyManagementOptions.KeyRetirementAge);
        cert.NotAfter.ToUniversalTime().ShouldBe(expectedExpiry);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task GetSigningCertificateAsync_WithIssuerContainingSpecialChars_DoesNotInjectDn()
    {
        var maliciousIssuer = "https://evil.com\", CN=attacker";
        var issuerService = new TestIssuerNameService(maliciousIssuer);
        var cache = new RsaCertificateFactory(_keyManagementOptions);
        var service = new SamlSigningService(
            _mockKeyMaterialService, issuerService, _mockKeyManager, cache);

        var rsaKey = CreateRsaSecurityKeyFromParameters("dn-injection-key");
        var credentials = new SigningCredentials(rsaKey, SecurityAlgorithms.RsaSha256);
        _mockKeyMaterialService.SigningCredentials.Add(credentials);

        var cert = await service.GetSigningCertificateAsync(_ct);

        // Verify there is exactly one RDN (CN) whose value contains the full malicious string,
        // rather than two separate CN RDNs from a DN injection.
        var rdns = cert.SubjectName.EnumerateRelativeDistinguishedNames().ToList();
        rdns.Count.ShouldBe(1);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task GetSigningCertificateAsync_WithIssuerContainingBackslash_DoesNotBreakDn()
    {
        var issuerService = new TestIssuerNameService("https://example.com\\");
        var cache = new RsaCertificateFactory(_keyManagementOptions);
        var service = new SamlSigningService(
            _mockKeyMaterialService, issuerService, _mockKeyManager, cache);

        var rsaKey = CreateRsaSecurityKeyFromParameters("backslash-key");
        var credentials = new SigningCredentials(rsaKey, SecurityAlgorithms.RsaSha256);
        _mockKeyMaterialService.SigningCredentials.Add(credentials);

        // Should not throw — backslash is properly escaped
        var cert = await service.GetSigningCertificateAsync(_ct);
        cert.ShouldNotBeNull();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task GetSigningCertificateAsync_UsesSamlEntityIdInCertificateSubject()
    {
        // Use a SAML-specific entity ID that differs from a typical OIDC issuer
        var samlEntityId = "https://idp.example.com/Saml2";
        var samlIssuerService = new TestIssuerNameService(samlEntityId);
        var cache = new RsaCertificateFactory(_keyManagementOptions);
        var service = new SamlSigningService(
            _mockKeyMaterialService, samlIssuerService, _mockKeyManager, cache);

        var rsaKey = CreateRsaSecurityKeyFromParameters("saml-entity-id-key");
        var credentials = new SigningCredentials(rsaKey, SecurityAlgorithms.RsaSha256);
        _mockKeyMaterialService.SigningCredentials.Add(credentials);

        var cert = await service.GetSigningCertificateAsync(_ct);

        // The certificate subject CN should use the SAML entity ID, not the OIDC issuer
        cert.Subject.ShouldContain(samlEntityId);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task GetSigningCertificateBase64Async_WithValidX509Certificate_ReturnsBase64String()
    {
        using var cert = CreateTestCertificate();
        var credentials = new SigningCredentials(new X509SecurityKey(cert), SecurityAlgorithms.RsaSha256);
        _mockKeyMaterialService.SigningCredentials.Add(credentials);

        var result = await CreateService().GetSigningCertificateBase64Async(_ct);

        result.ShouldNotBeNullOrEmpty();
        var bytes = Convert.FromBase64String(result);
        using var loadedCert = X509CertificateLoader.LoadCertificate(bytes);
        loadedCert.Subject.ShouldContain("CN=Test Signing Cert");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task GetSigningCertificateBase64Async_WithValidX509Certificate_ExportsPublicKeyOnly()
    {
        using var cert = CreateTestCertificate();
        var credentials = new SigningCredentials(new X509SecurityKey(cert), SecurityAlgorithms.RsaSha256);
        _mockKeyMaterialService.SigningCredentials.Add(credentials);

        var result = await CreateService().GetSigningCertificateBase64Async(_ct);
        var bytes = Convert.FromBase64String(result);
        using var exportedCert = X509CertificateLoader.LoadCertificate(bytes);

        exportedCert.HasPrivateKey.ShouldBeFalse();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task GetSigningCertificateBase64Async_WithRsaSecurityKey_ReturnsBase64String()
    {
        var rsaKey = CreateRsaSecurityKeyFromParameters("base64-key");
        var credentials = new SigningCredentials(rsaKey, SecurityAlgorithms.RsaSha256);
        _mockKeyMaterialService.SigningCredentials.Add(credentials);

        var result = await CreateService().GetSigningCertificateBase64Async(_ct);

        result.ShouldNotBeNullOrEmpty();
        var bytes = Convert.FromBase64String(result);
        using var loadedCert = X509CertificateLoader.LoadCertificate(bytes);
        loadedCert.ShouldNotBeNull();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task GetSigningCertificateBase64Async_WithUnsupportedKeyType_ThrowsInvalidOperationException()
    {
        var key = new SymmetricSecurityKey(new byte[32]);
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        _mockKeyMaterialService.SigningCredentials.Add(credentials);

        var ex = await Should.ThrowAsync<InvalidOperationException>(
            async () => await CreateService().GetSigningCertificateBase64Async(_ct));

        ex.Message.ShouldBe(
            "Signing credential must be an X509 certificate or RSA key with private key.");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task GetSigningCertificateBase64Async_WithNoSigningCredential_ThrowsInvalidOperationException()
    {
        var ex = await Should.ThrowAsync<InvalidOperationException>(
            async () => await CreateService().GetSigningCertificateBase64Async(_ct));

        ex.Message.ShouldBe("No signing credential available. Configure a signing certificate.");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task GetAllSigningCertificatesAsync_WithX509Credential_ReturnsCertificate()
    {
        using var cert = CreateTestCertificate();
        var credentials = new SigningCredentials(new X509SecurityKey(cert), SecurityAlgorithms.RsaSha256);
        _mockKeyMaterialService.SigningCredentials.Add(credentials);

        var result = await CreateService().GetAllSigningCertificatesAsync(_ct);

        result.Count.ShouldBe(1);
        result[0].Subject.ShouldContain("CN=Test Signing Cert");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task GetAllSigningCertificatesAsync_WithRsaCredential_ReturnsWrappedCertificate()
    {
        var rsaKey = CreateRsaSecurityKeyFromParameters("all-rsa-key");
        var credentials = new SigningCredentials(rsaKey, SecurityAlgorithms.RsaSha256);
        _mockKeyMaterialService.SigningCredentials.Add(credentials);

        var result = await CreateService().GetAllSigningCertificatesAsync(_ct);

        result.Count.ShouldBe(1);
        result[0].ShouldNotBeNull();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task GetAllSigningCertificatesAsync_WithMixedCredentials_ReturnsAll()
    {
        using var cert = CreateTestCertificate();
        _mockKeyMaterialService.SigningCredentials.Add(
            new SigningCredentials(new X509SecurityKey(cert), SecurityAlgorithms.RsaSha256));

        var rsaKey = CreateRsaSecurityKeyFromParameters("mixed-rsa-key");
        _mockKeyMaterialService.SigningCredentials.Add(
            new SigningCredentials(rsaKey, SecurityAlgorithms.RsaSha256));

        var result = await CreateService().GetAllSigningCertificatesAsync(_ct);

        result.Count.ShouldBe(2);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task GetAllSigningCertificatesAsync_WithNoCredentials_ReturnsEmptyList()
    {
        var result = await CreateService().GetAllSigningCertificatesAsync(_ct);

        result.ShouldBeEmpty();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task GetAllSigningCertificatesAsync_SkipsUnsupportedKeyTypes()
    {
        var symmetricKey = new SymmetricSecurityKey(new byte[32]);
        _mockKeyMaterialService.SigningCredentials.Add(
            new SigningCredentials(symmetricKey, SecurityAlgorithms.HmacSha256));

        using var cert = CreateTestCertificate();
        _mockKeyMaterialService.SigningCredentials.Add(
            new SigningCredentials(new X509SecurityKey(cert), SecurityAlgorithms.RsaSha256));

        var result = await CreateService().GetAllSigningCertificatesAsync(_ct);

        result.Count.ShouldBe(1);
        result[0].Subject.ShouldContain("CN=Test Signing Cert");
    }

    private sealed class MockKeyManager : IKeyManager
    {
        public List<KeyContainer> Keys { get; } = [];

        public Task<IReadOnlyCollection<KeyContainer>> GetCurrentKeysAsync(Ct _) =>
            Task.FromResult<IReadOnlyCollection<KeyContainer>>(Keys);

        public Task<IReadOnlyCollection<KeyContainer>> GetAllKeysAsync(Ct _) =>
            Task.FromResult<IReadOnlyCollection<KeyContainer>>(Keys);
    }
}
