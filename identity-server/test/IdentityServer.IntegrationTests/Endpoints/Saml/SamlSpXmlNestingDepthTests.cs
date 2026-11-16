// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Xml;
using Duende.IdentityServer.Hosting.DynamicProviders;
using Duende.IdentityServer.IntegrationTests.Endpoints.Saml.DynamicProvider;
using Duende.IdentityServer.IntegrationTests.TestFramework;
using Duende.IdentityServer.Internal.Saml.Sp;
using Duende.IdentityServer.Internal.Saml.Sp.AspNetCore;
using Duende.IdentityServer.Internal.Saml.Sp.Helpers;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Saml.Configuration;
using Duende.IdentityServer.Saml.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Duende.IdentityServer.IntegrationTests.Endpoints.Saml;

public class SamlSpXmlNestingDepthTests(ITestOutputHelper output)
{
    private const string Category = "SAML SP XML nesting depth";
    private const string AcsPath = "/federation/saml-idp/Saml2/Acs";
    private const string LogoutPath = "/federation/saml-idp/Saml2/Logout";
    private const int DefaultMaxDepth = 32;
    private const int ConfiguredMaxDepth = 8;

    private readonly Ct _ct = TestContext.Current.CancellationToken;

    [Fact]
    [Trait("Category", Category)]
    public async Task error_when_acs_post_exceeds_default_nesting_depth()
    {
        var recorder = new ExceptionRecorder();
        await using var fixture = await CreateFixtureAsync(recorder);

        var response = await PostAsync(fixture.SpHost!, AcsPath, "SAMLResponse", ResponseNestedTo(DefaultMaxDepth + 1));

        SamlTestHelpers.ShouldBeMalformedXmlRejection(response, recorder);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task error_when_logout_redirect_exceeds_default_nesting_depth()
    {
        var recorder = new ExceptionRecorder();
        await using var fixture = await CreateFixtureAsync(recorder);

        var encoded = await SamlTestHelpers.EncodeRequest(LogoutRequestNestedTo(DefaultMaxDepth + 1), _ct);
        using var client = fixture.SpHost!.CreateClient(allowAutoRedirect: false);
        var response = await client.GetAsync($"{LogoutPath}?SAMLRequest={encoded}", _ct);

        SamlTestHelpers.ShouldBeMalformedXmlRejection(response, recorder);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task error_when_logout_post_exceeds_default_nesting_depth()
    {
        var recorder = new ExceptionRecorder();
        await using var fixture = await CreateFixtureAsync(recorder);

        var response = await PostAsync(fixture.SpHost!, LogoutPath, "SAMLRequest", LogoutRequestNestedTo(DefaultMaxDepth + 1));

        SamlTestHelpers.ShouldBeMalformedXmlRejection(response, recorder);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task error_when_dynamic_provider_acs_post_exceeds_configured_nesting_depth()
    {
        var recorder = new ExceptionRecorder();
        await using var fixture = await CreateFixtureAsync(recorder,
            services => services.ConfigureOptions<ConfiguredNestingDepth>());

        var response = await PostAsync(fixture.SpHost!, AcsPath, "SAMLResponse", ResponseNestedTo(ConfiguredMaxDepth + 1));

        SamlTestHelpers.ShouldBeMalformedXmlRejection(response, recorder);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task error_when_static_sp_acs_post_exceeds_configured_nesting_depth()
    {
        var recorder = new ExceptionRecorder();
        await using var host = await KestrelTestHost.Create(output,
            services =>
            {
                services.AddSingleton<IStartupFilter>(recorder);
                services.AddIdentityServer();
                services.AddAuthentication().AddSamlServiceProvider(options =>
                {
                    options.SpEntityId = "https://static-sp.example.com";
                    options.IdpEntityId = "https://idp.example.com";
                    options.SingleSignOnServiceUrl = "https://idp.example.com/sso";
                    options.MaxXmlNestingDepth = ConfiguredMaxDepth;
                });
            },
            _ => { },
            _ct);

        var response = await PostAsync(host, $"{SamlServiceProviderDefaults.ModulePath}/Acs", "SAMLResponse",
            ResponseNestedTo(ConfiguredMaxDepth + 1));

        SamlTestHelpers.ShouldBeMalformedXmlRejection(response, recorder);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task error_when_acs_post_decrypted_assertion_exceeds_configured_nesting_depth()
    {
        RegisterAesGcmAlgorithms();
        using var encryptionCertificate = CreateEncryptionCertificate();
        var recorder = new ExceptionRecorder();
        await using var fixture = await CreateFixtureAsync(recorder, services =>
        {
            services.ConfigureOptions<ConfiguredNestingDepth>();
            services.AddSingleton<IConfigureOptions<Saml2Options>>(
                new ConfigureDecryptionCertificate(encryptionCertificate));
        });

        var idpEntityId = $"{fixture.IdpHost!.Uri().ToString().TrimEnd('/')}/Saml2";
        var xml = EncryptedResponseNestedTo(ConfiguredMaxDepth + 1, idpEntityId, encryptionCertificate);
        SecureXmlParser.LoadXmlDocument(xml, int.MaxValue, ConfiguredMaxDepth).ShouldNotBeNull();
        var response = await PostAsync(fixture.SpHost!, AcsPath, "SAMLResponse", xml);

        SamlTestHelpers.ShouldBeMalformedXmlRejection(response, recorder);
    }

    private async Task<SamlDynamicProviderFixture> CreateFixtureAsync(
        ExceptionRecorder recorder,
        Action<IServiceCollection>? additionalSpServices = null)
    {
        var fixture = new SamlDynamicProviderFixture(output,
            additionalSpServices: services =>
            {
                services.AddSingleton<IStartupFilter>(recorder);
                additionalSpServices?.Invoke(services);
            });
        await fixture.InitializeAsync();
        return fixture;
    }

    private static X509Certificate2 CreateEncryptionCertificate()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=SP Encryption", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddHours(1));
        return X509CertificateLoader.LoadPkcs12(
            certificate.Export(X509ContentType.Pfx),
            null,
            X509KeyStorageFlags.Exportable);
    }

    private static string EncryptedResponseNestedTo(
        int deepestElementDepth,
        string idpEntityId,
        X509Certificate2 encryptionCertificate)
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

        var document = XmlHelpers.XmlDocumentFromString(
            $"""
             <samlp:Response xmlns:samlp="urn:oasis:names:tc:SAML:2.0:protocol"
                             xmlns:saml="urn:oasis:names:tc:SAML:2.0:assertion"
                             ID="_{Guid.NewGuid():N}" Version="2.0"
                             IssueInstant="{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ}">
               <saml:Issuer>{idpEntityId}</saml:Issuer>
               <samlp:Status>
                 <samlp:StatusCode Value="urn:oasis:names:tc:SAML:2.0:status:Success" />
               </samlp:Status>
               <saml:EncryptedAssertion>
                 <saml:Assertion ID="_{Guid.NewGuid():N}" Version="2.0"
                                 IssueInstant="{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ}">
                   <saml:Issuer>{idpEntityId}</saml:Issuer>
                   <saml:Subject><saml:NameID>user</saml:NameID></saml:Subject>
                   {nested}
                 </saml:Assertion>
               </saml:EncryptedAssertion>
             </samlp:Response>
             """);

        var assertion = (XmlElement)document.GetElementsByTagName(
            "Assertion",
            "urn:oasis:names:tc:SAML:2.0:assertion")[0]!;
        assertion.Encrypt(useOaep: true, encryptionCertificate);

        return document.OuterXml;
    }

    private static void RegisterAesGcmAlgorithms()
    {
        if (CryptoConfig.CreateFromName(AesGcmAlgorithm.AesGcm128Identifier) == null)
        {
            CryptoConfig.AddAlgorithm(typeof(IntegrationTestAesGcmAlgorithm128), AesGcmAlgorithm.AesGcm128Identifier);
        }

        if (CryptoConfig.CreateFromName(AesGcmAlgorithm.AesGcm256Identifier) == null)
        {
            CryptoConfig.AddAlgorithm(typeof(IntegrationTestAesGcmAlgorithm256), AesGcmAlgorithm.AesGcm256Identifier);
        }
    }

    private sealed class ConfigureDecryptionCertificate(X509Certificate2 certificate)
        : IConfigureNamedOptions<Saml2Options>
    {
        public void Configure(Saml2Options options)
        {
        }

        public void Configure(string? name, Saml2Options options)
        {
            if (name == "saml-idp")
            {
                options.SPOptions.ServiceCertificates.Add(new ServiceCertificate
                {
                    Certificate = certificate,
                    Use = CertificateUse.Encryption
                });
            }
        }
    }

    private async Task<HttpResponseMessage> PostAsync(KestrelTestHost host, string path, string parameter, string xml)
    {
        using var client = host.CreateClient(allowAutoRedirect: false);
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            [parameter] = SamlTestHelpers.ConvertToBase64Encoded(xml)
        });
        return await client.PostAsync(path, content, _ct);
    }

    private static string ResponseNestedTo(int deepestElementDepth) =>
        $"""
         <samlp:Response xmlns:samlp="urn:oasis:names:tc:SAML:2.0:protocol"
                         xmlns:saml="urn:oasis:names:tc:SAML:2.0:assertion"
                         ID="_{Guid.NewGuid():N}" Version="2.0"
                         IssueInstant="{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ}">
           <saml:Issuer>https://idp.example.com</saml:Issuer>
           {SamlTestHelpers.ExtensionsNestedTo(deepestElementDepth)}
           <samlp:Status>
             <samlp:StatusCode Value="urn:oasis:names:tc:SAML:2.0:status:Success" />
           </samlp:Status>
         </samlp:Response>
         """;

    private static string LogoutRequestNestedTo(int deepestElementDepth) =>
        $"""
         <samlp:LogoutRequest xmlns:samlp="urn:oasis:names:tc:SAML:2.0:protocol"
                              xmlns:saml="urn:oasis:names:tc:SAML:2.0:assertion"
                              ID="_{Guid.NewGuid():N}" Version="2.0"
                              IssueInstant="{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ}">
           <saml:Issuer>https://idp.example.com</saml:Issuer>
           {SamlTestHelpers.ExtensionsNestedTo(deepestElementDepth)}
           <saml:NameID>user</saml:NameID>
           <samlp:SessionIndex>session</samlp:SessionIndex>
         </samlp:LogoutRequest>
         """;

    private sealed class ConfiguredNestingDepth(
        IHttpContextAccessor httpContextAccessor,
        ILogger<ConfiguredNestingDepth> logger)
        : ConfigureAuthenticationOptions<SamlAuthenticationOptions, SamlProvider>(httpContextAccessor, logger)
    {
        protected override void Configure(ConfigureAuthenticationContext<SamlAuthenticationOptions, SamlProvider> context) =>
            context.AuthenticationOptions.MaxXmlNestingDepth = ConfiguredMaxDepth;
    }
}

public class IntegrationTestAesGcmAlgorithm : SymmetricAlgorithm
{
    private readonly SymmetricAlgorithm _inner;

    protected IntegrationTestAesGcmAlgorithm(SymmetricAlgorithm inner)
    {
        _inner = inner;
        LegalKeySizesValue = inner.LegalKeySizes;
        LegalBlockSizesValue = inner.LegalBlockSizes;
        BlockSizeValue = inner.BlockSize;
        IV = inner.IV;
    }

    public override ICryptoTransform CreateDecryptor(byte[] rgbKey, byte[]? rgbIV) => _inner.CreateDecryptor(rgbKey, rgbIV);

    public override ICryptoTransform CreateEncryptor(byte[] rgbKey, byte[]? rgbIV) => _inner.CreateEncryptor(rgbKey, rgbIV);

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

public sealed class IntegrationTestAesGcmAlgorithm128() : IntegrationTestAesGcmAlgorithm(new AesGcmAlgorithm128());

public sealed class IntegrationTestAesGcmAlgorithm256() : IntegrationTestAesGcmAlgorithm(new AesGcmAlgorithm256());
