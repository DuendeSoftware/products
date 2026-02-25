// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Duende.IdentityServer.EntityFramework.Mappers;
using Duende.IdentityServer.Models;
using Models = Duende.IdentityServer.Models;

namespace IdentityServer.UnitTests.Mappers;

public class SamlServiceProviderMappersTests
{
    private static X509Certificate2 CreateSelfSignedCert()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=Test",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        return request.CreateSelfSigned(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddYears(1));
    }

    [Fact]
    public void Can_Map_minimal_sp()
    {
        var model = new Models.SamlServiceProvider
        {
            EntityId = "https://sp.example.com",
            AssertionConsumerServiceUrls = new HashSet<Uri> { new Uri("https://sp.example.com/acs") }
        };

        var entity = model.ToEntity();
        var roundTripped = entity.ToModel();

        roundTripped.ShouldNotBeNull();
        entity.ShouldNotBeNull();
        roundTripped.EntityId.ShouldBe(model.EntityId);
    }

    [Fact]
    public void EntityId_maps_correctly()
    {
        var model = new Models.SamlServiceProvider { EntityId = "https://sp.example.com" };

        var entity = model.ToEntity();

        entity.EntityId.ShouldBe("https://sp.example.com");
        entity.ToModel().EntityId.ShouldBe("https://sp.example.com");
    }

    [Fact]
    public void Enabled_maps_correctly()
    {
        var model = new Models.SamlServiceProvider { EntityId = "x", Enabled = false };

        var entity = model.ToEntity();
        var roundTripped = entity.ToModel();

        entity.Enabled.ShouldBeFalse();
        roundTripped.Enabled.ShouldBeFalse();
    }

    [Fact]
    public void TimeSpan_ClockSkew_maps_to_ticks_and_back()
    {
        var ts = TimeSpan.FromMinutes(5);
        var model = new Models.SamlServiceProvider { EntityId = "x", ClockSkew = ts };

        var entity = model.ToEntity();
        var roundTripped = entity.ToModel();

        entity.ClockSkewTicks.ShouldBe(ts.Ticks);
        roundTripped.ClockSkew.ShouldBe(ts);
    }

    [Fact]
    public void Null_TimeSpan_ClockSkew_maps_to_null()
    {
        var model = new Models.SamlServiceProvider { EntityId = "x", ClockSkew = null };

        var entity = model.ToEntity();
        var roundTripped = entity.ToModel();

        entity.ClockSkewTicks.ShouldBeNull();
        roundTripped.ClockSkew.ShouldBeNull();
    }

    [Fact]
    public void TimeSpan_RequestMaxAge_maps_to_ticks_and_back()
    {
        var ts = TimeSpan.FromSeconds(300);
        var model = new Models.SamlServiceProvider { EntityId = "x", RequestMaxAge = ts };

        var entity = model.ToEntity();
        var roundTripped = entity.ToModel();

        entity.RequestMaxAgeTicks.ShouldBe(ts.Ticks);
        roundTripped.RequestMaxAge.ShouldBe(ts);
    }

    [Fact]
    public void ACS_urls_map_correctly()
    {
        var urls = new HashSet<Uri>
        {
            new Uri("https://sp.example.com/acs1"),
            new Uri("https://sp.example.com/acs2")
        };
        var model = new Models.SamlServiceProvider { EntityId = "x", AssertionConsumerServiceUrls = urls };

        var entity = model.ToEntity();
        var roundTripped = entity.ToModel();

        entity.AssertionConsumerServiceUrls.Count.ShouldBe(2);
        entity.AssertionConsumerServiceUrls.ShouldContain(a => a.Url == "https://sp.example.com/acs1");
        entity.AssertionConsumerServiceUrls.ShouldContain(a => a.Url == "https://sp.example.com/acs2");

        roundTripped.AssertionConsumerServiceUrls.Count.ShouldBe(2);
        roundTripped.AssertionConsumerServiceUrls.ShouldContain(u => u.AbsoluteUri == "https://sp.example.com/acs1");
        roundTripped.AssertionConsumerServiceUrls.ShouldContain(u => u.AbsoluteUri == "https://sp.example.com/acs2");
    }

    [Fact]
    public void SamlEndpointType_SingleLogoutServiceUrl_maps_correctly()
    {
        var endpoint = new SamlEndpointType
        {
            Location = new Uri("https://sp.example.com/slo"),
            Binding = SamlBinding.HttpPost
        };
        var model = new Models.SamlServiceProvider { EntityId = "x", SingleLogoutServiceUrl = endpoint };

        var entity = model.ToEntity();
        var roundTripped = entity.ToModel();

        entity.SingleLogoutServiceUrl.ShouldBe("https://sp.example.com/slo");
        entity.SingleLogoutServiceBinding.ShouldBe((int)SamlBinding.HttpPost);

        roundTripped.SingleLogoutServiceUrl.ShouldNotBeNull();
        roundTripped.SingleLogoutServiceUrl!.Location.AbsoluteUri.ShouldBe("https://sp.example.com/slo");
        roundTripped.SingleLogoutServiceUrl.Binding.ShouldBe(SamlBinding.HttpPost);
    }

    [Fact]
    public void Null_SingleLogoutServiceUrl_maps_correctly()
    {
        var model = new Models.SamlServiceProvider { EntityId = "x", SingleLogoutServiceUrl = null };

        var entity = model.ToEntity();
        var roundTripped = entity.ToModel();

        entity.SingleLogoutServiceUrl.ShouldBeNull();
        entity.SingleLogoutServiceBinding.ShouldBeNull();
        roundTripped.SingleLogoutServiceUrl.ShouldBeNull();
    }

    [Fact]
    public void Signing_certificates_map_to_base64_and_back()
    {
        var cert = CreateSelfSignedCert();
        var model = new Models.SamlServiceProvider
        {
            EntityId = "x",
            SigningCertificates = new List<X509Certificate2> { cert }
        };

        var entity = model.ToEntity();
        var roundTripped = entity.ToModel();

        entity.SigningCertificates.Count.ShouldBe(1);
        entity.SigningCertificates[0].Data.ShouldBe(Convert.ToBase64String(cert.RawData));

        roundTripped.SigningCertificates!.Count.ShouldBe(1);
        roundTripped.SigningCertificates.First().RawData.ShouldBe(cert.RawData);
    }

    [Fact]
    public void Encryption_certificates_map_to_base64_and_back()
    {
        var cert = CreateSelfSignedCert();
        var model = new Models.SamlServiceProvider
        {
            EntityId = "x",
            EncryptionCertificates = new List<X509Certificate2> { cert }
        };

        var entity = model.ToEntity();
        var roundTripped = entity.ToModel();

        entity.EncryptionCertificates.Count.ShouldBe(1);
        entity.EncryptionCertificates[0].Data.ShouldBe(Convert.ToBase64String(cert.RawData));

        roundTripped.EncryptionCertificates!.Count.ShouldBe(1);
        roundTripped.EncryptionCertificates.First().RawData.ShouldBe(cert.RawData);
    }

    [Fact]
    public void ClaimMappings_map_correctly()
    {
        var model = new Models.SamlServiceProvider
        {
            EntityId = "x",
            ClaimMappings = new Dictionary<string, string>
            {
                { "department", "businessUnit" },
                { "email", "mail" }
            }
        };

        var entity = model.ToEntity();
        var roundTripped = entity.ToModel();

        entity.ClaimMappings.Count.ShouldBe(2);
        entity.ClaimMappings.ShouldContain(m => m.ClaimType == "department" && m.SamlAttributeName == "businessUnit");
        entity.ClaimMappings.ShouldContain(m => m.ClaimType == "email" && m.SamlAttributeName == "mail");

        roundTripped.ClaimMappings.Count.ShouldBe(2);
        roundTripped.ClaimMappings["department"].ShouldBe("businessUnit");
        roundTripped.ClaimMappings["email"].ShouldBe("mail");
    }

    [Fact]
    public void SigningBehavior_maps_correctly()
    {
        var model = new Models.SamlServiceProvider
        {
            EntityId = "x",
            SigningBehavior = SamlSigningBehavior.SignBoth
        };

        var entity = model.ToEntity();
        var roundTripped = entity.ToModel();

        entity.SigningBehavior.ShouldBe((int)SamlSigningBehavior.SignBoth);
        roundTripped.SigningBehavior.ShouldBe(SamlSigningBehavior.SignBoth);
    }

    [Fact]
    public void Null_SigningBehavior_maps_correctly()
    {
        var model = new Models.SamlServiceProvider { EntityId = "x", SigningBehavior = null };

        var entity = model.ToEntity();
        var roundTripped = entity.ToModel();

        entity.SigningBehavior.ShouldBeNull();
        roundTripped.SigningBehavior.ShouldBeNull();
    }

    [Fact]
    public void AssertionConsumerServiceBinding_enum_maps_correctly()
    {
        var model = new Models.SamlServiceProvider
        {
            EntityId = "x",
            AssertionConsumerServiceBinding = SamlBinding.HttpPost
        };

        var entity = model.ToEntity();
        var roundTripped = entity.ToModel();

        entity.AssertionConsumerServiceBinding.ShouldBe((int)SamlBinding.HttpPost);
        roundTripped.AssertionConsumerServiceBinding.ShouldBe(SamlBinding.HttpPost);
    }
}
