// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Xml;
using Duende.IdentityServer.Internal.Saml.Sp;
using Duende.IdentityServer.Internal.Saml.Sp.Configuration;
using Duende.IdentityServer.Internal.Saml.Sp.Exceptions;
using Duende.IdentityServer.Internal.Saml.Sp.Helpers;
using Duende.IdentityServer.Internal.Saml.Sp.Metadata;
using Duende.IdentityServer.Internal.Saml.Sp.Protocol;

namespace UnitTests.Saml.Sp;

public sealed class Saml2ResponseTests
{
    private const string IdpEntityId = "https://idp.example.com";
    private const int MaxDepth = 4;

    // Response is depth 0, EncryptedAssertion is depth 1 and Assertion is depth 2.
    [Fact]
    public void error_when_decrypted_assertion_exceeds_configured_nesting_depth()
    {
        var (response, options) = CreateEncryptedResponse(deepestElementDepth: MaxDepth + 1);

        Should.Throw<XmlException>(() => response.GetClaims(options, null, new Uri("https://sp.example.com/Saml2/Acs")));
    }

    [Fact]
    public void success_when_decrypted_assertion_is_nested_to_configured_depth()
    {
        var (response, options) = CreateEncryptedResponse(deepestElementDepth: MaxDepth);

        // Decryption succeeded, so processing reaches the signature check on the decrypted assertion.
        var exception = Should.Throw<Saml2ResponseFailedValidationException>(() => response.GetClaims(options, null, new Uri("https://sp.example.com/Saml2/Acs")));
        exception.Message.ShouldContain("contains unsigned Assertions");
    }

    private static (Saml2Response Response, Options Options) CreateEncryptedResponse(int deepestElementDepth)
    {
        RegisterAesGcmAlgorithms();

        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=SP Encryption", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddHours(1));

        var spOptions = new SPOptions { MaxXmlNestingDepth = MaxDepth };
        spOptions.ServiceCertificates.Add(new ServiceCertificate { Certificate = certificate, Use = CertificateUse.Encryption });
        var options = new Options(spOptions);
        options.IdentityProviders.Add(new IdentityProvider(new EntityId(IdpEntityId), spOptions, TimeProvider.System)
        {
            AllowUnsolicitedAuthnResponse = true
        });

        var document = XmlHelpers.XmlDocumentFromString(ResponseXml(deepestElementDepth));
        var assertion = (XmlElement)document.GetElementsByTagName("Assertion", "urn:oasis:names:tc:SAML:2.0:assertion")[0]!;
        assertion.Encrypt(useOaep: true, certificate);

        return (new Saml2Response(document.DocumentElement!, expectedInResponseTo: null), options);
    }

    // RSAEncryptedXml registers its internal AES-GCM types with CryptoConfig, which only accepts
    // public types. Registering public wrappers first lets the existing decryption path run.
    private static void RegisterAesGcmAlgorithms()
    {
        if (CryptoConfig.CreateFromName(AesGcmAlgorithm.AesGcm128Identifier) == null)
        {
            CryptoConfig.AddAlgorithm(typeof(TestAesGcmAlgorithm128), AesGcmAlgorithm.AesGcm128Identifier);
        }

        if (CryptoConfig.CreateFromName(AesGcmAlgorithm.AesGcm256Identifier) == null)
        {
            CryptoConfig.AddAlgorithm(typeof(TestAesGcmAlgorithm256), AesGcmAlgorithm.AesGcm256Identifier);
        }
    }

    private static string ResponseXml(int deepestElementDepth)
    {
        var nested = new StringBuilder();
        var count = deepestElementDepth - 2;
        for (var i = 0; i < count; i++)
        {
            nested.Append("<n xmlns=\"urn:test:nesting\">");
        }

        nested.Append("value");
        for (var i = 0; i < count; i++)
        {
            nested.Append("</n>");
        }

        return $"""
                <samlp:Response xmlns:samlp="urn:oasis:names:tc:SAML:2.0:protocol"
                                xmlns:saml="urn:oasis:names:tc:SAML:2.0:assertion"
                                ID="_{Guid.NewGuid():N}" Version="2.0"
                                IssueInstant="{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ}">
                  <saml:Issuer>{IdpEntityId}</saml:Issuer>
                  <samlp:Status>
                    <samlp:StatusCode Value="urn:oasis:names:tc:SAML:2.0:status:Success" />
                  </samlp:Status>
                  <saml:EncryptedAssertion><saml:Assertion>{nested}</saml:Assertion></saml:EncryptedAssertion>
                </samlp:Response>
                """;
    }
}

public class TestAesGcmAlgorithm : SymmetricAlgorithm
{
    private readonly SymmetricAlgorithm _inner;

    protected TestAesGcmAlgorithm(SymmetricAlgorithm inner)
    {
        _inner = inner;
        LegalKeySizesValue = inner.LegalKeySizes;
        LegalBlockSizesValue = inner.LegalBlockSizes;
        BlockSizeValue = inner.BlockSize;
        IV = inner.IV;
    }

    public override ICryptoTransform CreateDecryptor(byte[] rgbKey, byte[] rgbIV) => _inner.CreateDecryptor(rgbKey, rgbIV);

    public override ICryptoTransform CreateEncryptor(byte[] rgbKey, byte[] rgbIV) => _inner.CreateEncryptor(rgbKey, rgbIV);

    public override void GenerateIV() => _inner.GenerateIV();

    public override void GenerateKey() => _inner.GenerateKey();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _inner.Dispose();
        }

        base.Dispose(disposing);
    }
}

public sealed class TestAesGcmAlgorithm128() : TestAesGcmAlgorithm(new AesGcmAlgorithm128());

public sealed class TestAesGcmAlgorithm256() : TestAesGcmAlgorithm(new AesGcmAlgorithm256());
