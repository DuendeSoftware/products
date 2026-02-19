// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Xml.Linq;
using Duende.IdentityServer.Internal.Saml.Infrastructure;
using Duende.IdentityServer.Internal.Saml.SingleLogout;
using Duende.IdentityServer.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace UnitTests.Saml;

public class SamlFrontChannelLogoutRequestBuilderTests
{
    private const string Category = "SAML Front Channel Logout Request Builder";

    private readonly FakeTimeProvider _timeProvider;
    private readonly SamlProtocolMessageSigner _signer;
    private readonly SamlFrontChannelLogoutRequestBuilder _subject;

    public SamlFrontChannelLogoutRequestBuilderTests()
    {
        _timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 2, 5, 12, 0, 0, TimeSpan.Zero));
        _signer = CreateSigner();
        _subject = new SamlFrontChannelLogoutRequestBuilder(_timeProvider, _signer);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task service_provider_with_no_single_logout_url_should_throw_exception()
    {
        var sp = CreateServiceProvider();
        sp.SingleLogoutServiceUrl = null;

        await Should.ThrowAsync<InvalidOperationException>(async () =>
            await _subject.BuildLogoutRequestAsync(sp, "user@example.com", null, "session123", "https://idp.example.com")
        );
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task http_redirect_binding_should_return_redirect_logout()
    {
        var sp = CreateServiceProvider();

        var result = await _subject.BuildLogoutRequestAsync(
            sp,
            "user@example.com",
            "urn:oasis:names:tc:SAML:1.1:nameid-format:emailAddress",
            "session123",
            "https://idp.example.com");

        result.SamlBinding.ShouldBe(SamlBinding.HttpRedirect);
        result.Destination.ShouldBe(sp.SingleLogoutServiceUrl!.Location);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task http_post_binding_should_return_post_logout()
    {
        var sp = CreateServiceProvider();
        sp.SingleLogoutServiceUrl = new SamlEndpointType
        {
            Binding = SamlBinding.HttpPost,
            Location = new Uri("https://sp.example.com/slo")
        };

        var result = await _subject.BuildLogoutRequestAsync(
            sp,
            "user@example.com",
            "urn:oasis:names:tc:SAML:1.1:nameid-format:emailAddress",
            "session123",
            "https://idp.example.com");

        result.SamlBinding.ShouldBe(SamlBinding.HttpPost);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task unsupported_binding_should_throw_exception()
    {
        var sp = CreateServiceProvider();
        sp.SingleLogoutServiceUrl = new SamlEndpointType
        {
            Binding = (SamlBinding)999, // Unsupported binding
            Location = new Uri("https://sp.example.com/slo")
        };

        await Should.ThrowAsync<InvalidOperationException>(async () =>
            await _subject.BuildLogoutRequestAsync(sp, "user@example.com", null, "session123", "https://idp.example.com")
        );
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task http_redirect_should_encode_and_compress_request()
    {
        var sp = CreateServiceProvider();

        var result = await _subject.BuildLogoutRequestAsync(
            sp,
            "user@example.com",
            "urn:oasis:names:tc:SAML:1.1:nameid-format:emailAddress",
            "session123",
            "https://idp.example.com");

        result.EncodedContent.ShouldNotBeNullOrEmpty();
        result.EncodedContent.ShouldContain("SAMLRequest=");
        result.EncodedContent.ShouldContain("&SigAlg=");
        result.EncodedContent.ShouldContain("&Signature=");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task http_redirect_should_be_decodable_and_decompressible()
    {
        var sp = CreateServiceProvider();

        var result = await _subject.BuildLogoutRequestAsync(
            sp,
            "user@example.com",
            "urn:oasis:names:tc:SAML:1.1:nameid-format:emailAddress",
            "session123",
            "https://idp.example.com");

        var queryString = result.EncodedContent;
        var samlRequestPart = queryString.Split('&')[0].Replace("?SAMLRequest=", "");
        var decodedBytes = Convert.FromBase64String(Uri.UnescapeDataString(samlRequestPart));

        using var input = new MemoryStream(decodedBytes);
        using var deflateStream = new DeflateStream(input, CompressionMode.Decompress);
        using var reader = new StreamReader(deflateStream);
        var xml = await reader.ReadToEndAsync();

        xml.ShouldContain("<LogoutRequest");
        xml.ShouldContain("user@example.com");
        xml.ShouldContain("session123");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task http_post_should_encode_request_as_base64()
    {
        var sp = CreateServiceProvider();
        sp.SingleLogoutServiceUrl = new SamlEndpointType
        {
            Binding = SamlBinding.HttpPost,
            Location = new Uri("https://sp.example.com/slo")
        };

        var result = await _subject.BuildLogoutRequestAsync(
            sp,
            "user@example.com",
            "urn:oasis:names:tc:SAML:1.1:nameid-format:emailAddress",
            "session123",
            "https://idp.example.com");

        result.EncodedContent.ShouldNotBeNullOrEmpty();

        var decodedBytes = Convert.FromBase64String(result.EncodedContent);
        var xml = Encoding.UTF8.GetString(decodedBytes);

        xml.ShouldContain("<LogoutRequest");
        xml.ShouldContain("Signature");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task should_set_correct_issue_instant()
    {
        var sp = CreateServiceProvider();
        var expectedTime = _timeProvider.GetUtcNow().UtcDateTime;

        var result = await _subject.BuildLogoutRequestAsync(
            sp,
            "user@example.com",
            null,
            "session123",
            "https://idp.example.com");

        var xml = await DecodeRedirectRequest(result.EncodedContent);
        var expectedIssueInstant = expectedTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);
        xml.ShouldContain($"IssueInstant=\"{expectedIssueInstant}\"");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task should_set_correct_destination()
    {
        var sp = CreateServiceProvider();

        var result = await _subject.BuildLogoutRequestAsync(
            sp,
            "user@example.com",
            null,
            "session123",
            "https://idp.example.com");

        var xml = await DecodeRedirectRequest(result.EncodedContent);
        xml.ShouldContain($"Destination=\"{sp.SingleLogoutServiceUrl!.Location}\"");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task should_set_correct_issuer()
    {
        var sp = CreateServiceProvider();
        var issuer = "https://idp.example.com";

        var result = await _subject.BuildLogoutRequestAsync(
            sp,
            "user@example.com",
            null,
            "session123",
            issuer);

        var xml = await DecodeRedirectRequest(result.EncodedContent);
        xml.ShouldContain($"<Issuer xmlns=\"urn:oasis:names:tc:SAML:2.0:assertion\">{issuer}</Issuer>");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task should_set_correct_name_id()
    {
        var sp = CreateServiceProvider();
        var nameId = "user@example.com";

        var result = await _subject.BuildLogoutRequestAsync(
            sp,
            nameId,
            null,
            "session123",
            "https://idp.example.com");

        var xml = await DecodeRedirectRequest(result.EncodedContent);
        xml.ShouldContain($"<NameID xmlns=\"urn:oasis:names:tc:SAML:2.0:assertion\">{nameId}</NameID>");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task should_set_name_id_format_when_provided()
    {
        var sp = CreateServiceProvider();
        var nameIdFormat = "urn:oasis:names:tc:SAML:1.1:nameid-format:emailAddress";

        var result = await _subject.BuildLogoutRequestAsync(
            sp,
            "user@example.com",
            nameIdFormat,
            "session123",
            "https://idp.example.com");

        var xml = await DecodeRedirectRequest(result.EncodedContent);
        xml.ShouldContain($"Format=\"{nameIdFormat}\"");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task should_omit_name_id_format_when_null()
    {
        var sp = CreateServiceProvider();

        var result = await _subject.BuildLogoutRequestAsync(
            sp,
            "user@example.com",
            null,
            "session123",
            "https://idp.example.com");

        var xml = await DecodeRedirectRequest(result.EncodedContent);
        var doc = XDocument.Parse(xml);
        var nameIdElement = doc.Descendants().First(e => e.Name.LocalName == "NameID");
        nameIdElement.Attribute("Format").ShouldBeNull();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task should_set_correct_session_index()
    {
        var sp = CreateServiceProvider();
        var sessionIndex = "session123";

        var result = await _subject.BuildLogoutRequestAsync(
            sp,
            "user@example.com",
            null,
            sessionIndex,
            "https://idp.example.com");

        var xml = await DecodeRedirectRequest(result.EncodedContent);
        xml.ShouldContain($"<SessionIndex>{sessionIndex}</SessionIndex>");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task should_generate_unique_request_id()
    {
        var sp = CreateServiceProvider();

        var result1 = await _subject.BuildLogoutRequestAsync(sp, "user@example.com", null, "session123", "https://idp.example.com");
        var result2 = await _subject.BuildLogoutRequestAsync(sp, "user@example.com", null, "session123", "https://idp.example.com");

        var xml1 = await DecodeRedirectRequest(result1.EncodedContent);
        var xml2 = await DecodeRedirectRequest(result2.EncodedContent);

        var doc1 = XDocument.Parse(xml1);
        var doc2 = XDocument.Parse(xml2);
        var id1 = doc1.Root!.Attribute("ID")!.Value;
        var id2 = doc2.Root!.Attribute("ID")!.Value;

        id1.ShouldNotBe(id2);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task should_set_saml_version_2()
    {
        var sp = CreateServiceProvider();

        var result = await _subject.BuildLogoutRequestAsync(
            sp,
            "user@example.com",
            null,
            "session123",
            "https://idp.example.com");

        var xml = await DecodeRedirectRequest(result.EncodedContent);
        xml.ShouldContain("Version=\"2.0\"");
    }

    private static async Task<string> DecodeRedirectRequest(string encodedContent)
    {
        var samlRequestPart = encodedContent.Split('&')[0].Replace("?SAMLRequest=", "");
        var decodedBytes = Convert.FromBase64String(Uri.UnescapeDataString(samlRequestPart));

        using var input = new MemoryStream(decodedBytes);
        using var deflateStream = new DeflateStream(input, CompressionMode.Decompress);
        using var reader = new StreamReader(deflateStream);
        return await reader.ReadToEndAsync();
    }

    private static SamlServiceProvider CreateServiceProvider() => new SamlServiceProvider
    {
        EntityId = "https://sp.example.com",
        DisplayName = "Test Service Provider",
        AssertionConsumerServiceUrls = [new Uri("https://sp.example.com/acs")],
        SingleLogoutServiceUrl = new SamlEndpointType
        {
            Binding = SamlBinding.HttpRedirect,
            Location = new Uri("https://sp.example.com/slo")
        }
    };

    private static SamlProtocolMessageSigner CreateSigner()
    {
        var cert = CreateTestCertificate();
        var mockSigningService = new UnitTests.Common.MockSamlSigningService(cert);

        return new SamlProtocolMessageSigner(
            mockSigningService,
            NullLogger<SamlProtocolMessageSigner>.Instance);
    }

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
}
