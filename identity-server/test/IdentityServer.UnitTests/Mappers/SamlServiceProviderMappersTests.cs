// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Cryptography.X509Certificates;
using Duende.IdentityServer.EntityFramework.Mappers;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Saml.Bindings;
using Entities = Duende.IdentityServer.EntityFramework.Entities;
using Models = Duende.IdentityServer.Models;

namespace IdentityServer.UnitTests.Mappers;

public class SamlServiceProviderMappersTests
{
    [Fact]
    public void Can_Map()
    {
        var model = new Models.SamlServiceProvider
        {
            EntityId = "https://sp.example.com",
            DisplayName = "Test SP"
        };
        var mappedEntity = model.ToEntity();
        var mappedModel = mappedEntity.ToModel();

        mappedModel.ShouldNotBeNull();
        mappedEntity.ShouldNotBeNull();
    }

    [Fact]
    public void ClaimMappings_RoundTrip()
    {
        var model = new Models.SamlServiceProvider
        {
            EntityId = "https://sp.example.com",
            DisplayName = "Test SP",
            ClaimMappings = new Dictionary<string, string>
            {
                { "department", "businessUnit" },
                { "email", "mail" }
            }
        };

        var mappedEntity = model.ToEntity();

        mappedEntity.ClaimMappings.Count.ShouldBe(2);
        mappedEntity.ClaimMappings.ShouldContain(m => m.ClaimType == "department" && m.SamlAttributeName == "businessUnit");
        mappedEntity.ClaimMappings.ShouldContain(m => m.ClaimType == "email" && m.SamlAttributeName == "mail");

        var mappedModel = mappedEntity.ToModel();

        mappedModel.ClaimMappings.Count.ShouldBe(2);
        mappedModel.ClaimMappings.ContainsKey("department").ShouldBeTrue();
        mappedModel.ClaimMappings["department"].ShouldBe("businessUnit");
        mappedModel.ClaimMappings.ContainsKey("email").ShouldBeTrue();
        mappedModel.ClaimMappings["email"].ShouldBe("mail");
    }

    [Fact]
    public void AllowedScopes_RoundTrip()
    {
        var model = new Models.SamlServiceProvider
        {
            EntityId = "https://sp.example.com",
            DisplayName = "Test SP",
            AllowedScopes = new HashSet<string> { "openid", "profile", "email" }
        };

        var mappedEntity = model.ToEntity();

        mappedEntity.AllowedScopes.Count.ShouldBe(3);
        mappedEntity.AllowedScopes.ShouldContain(s => s.Scope == "openid");
        mappedEntity.AllowedScopes.ShouldContain(s => s.Scope == "profile");
        mappedEntity.AllowedScopes.ShouldContain(s => s.Scope == "email");

        var mappedModel = mappedEntity.ToModel();

        mappedModel.AllowedScopes.Count.ShouldBe(3);
        mappedModel.AllowedScopes.ShouldContain("openid");
        mappedModel.AllowedScopes.ShouldContain("profile");
        mappedModel.AllowedScopes.ShouldContain("email");
    }

    [Fact]
    public void AuthnContextMappings_RoundTrip()
    {
        var model = new Models.SamlServiceProvider
        {
            EntityId = "https://sp.example.com",
            DisplayName = "Test SP",
            AuthnContextMappings = new Dictionary<string, string>
            {
                { "pwd", "urn:oasis:names:tc:SAML:2.0:ac:classes:Password" },
                { "mfa", "urn:oasis:names:tc:SAML:2.0:ac:classes:MobileTwoFactorContract" }
            }
        };

        var mappedEntity = model.ToEntity();

        mappedEntity.AuthnContextMappings.Count.ShouldBe(2);
        mappedEntity.AuthnContextMappings.ShouldContain(m => m.OidcValue == "pwd" && m.SamlAuthnContextClassRef == "urn:oasis:names:tc:SAML:2.0:ac:classes:Password");
        mappedEntity.AuthnContextMappings.ShouldContain(m => m.OidcValue == "mfa" && m.SamlAuthnContextClassRef == "urn:oasis:names:tc:SAML:2.0:ac:classes:MobileTwoFactorContract");

        var mappedModel = mappedEntity.ToModel();

        mappedModel.AuthnContextMappings.Count.ShouldBe(2);
        mappedModel.AuthnContextMappings.ContainsKey("pwd").ShouldBeTrue();
        mappedModel.AuthnContextMappings["pwd"].ShouldBe("urn:oasis:names:tc:SAML:2.0:ac:classes:Password");
        mappedModel.AuthnContextMappings.ContainsKey("mfa").ShouldBeTrue();
        mappedModel.AuthnContextMappings["mfa"].ShouldBe("urn:oasis:names:tc:SAML:2.0:ac:classes:MobileTwoFactorContract");
    }

    [Fact]
    public void RequestedClaimTypes_RoundTrip()
    {
        var model = new Models.SamlServiceProvider
        {
            EntityId = "https://sp.example.com",
            DisplayName = "Test SP",
            RequestedClaimTypes = ["email", "name", "department"]
        };

        var mappedEntity = model.ToEntity();

        mappedEntity.RequestedClaimTypes.Count.ShouldBe(3);
        mappedEntity.RequestedClaimTypes.ShouldContain(r => r.ClaimType == "email");
        mappedEntity.RequestedClaimTypes.ShouldContain(r => r.ClaimType == "name");
        mappedEntity.RequestedClaimTypes.ShouldContain(r => r.ClaimType == "department");

        var mappedModel = mappedEntity.ToModel();

        mappedModel.RequestedClaimTypes.Count.ShouldBe(3);
        mappedModel.RequestedClaimTypes.ShouldContain("email");
        mappedModel.RequestedClaimTypes.ShouldContain("name");
        mappedModel.RequestedClaimTypes.ShouldContain("department");
    }

    [Fact]
    public void Certificates_RoundTrip()
    {
        var cert = MapperTestHelpers.CreateTestCertificate();

        var model = new Models.SamlServiceProvider
        {
            EntityId = "https://sp.example.com",
            DisplayName = "Test SP",
            Certificates = new List<ServiceProviderCertificate>
            {
                new ServiceProviderCertificate { Certificate = cert, Use = KeyUse.Signing },
                new ServiceProviderCertificate { Certificate = cert, Use = KeyUse.Encryption },
                new ServiceProviderCertificate { Certificate = cert, Use = KeyUse.Both }
            }
        };

        var entity = model.ToEntity();

        entity.Certificates.Count.ShouldBe(3);
        entity.Certificates.ShouldContain(c => c.Use == (int)KeyUse.Signing);
        entity.Certificates.ShouldContain(c => c.Use == (int)KeyUse.Encryption);
        entity.Certificates.ShouldContain(c => c.Use == (int)KeyUse.Both);

        var roundTripped = entity.ToModel();

        roundTripped.Certificates.ShouldNotBeNull();
        roundTripped.Certificates.Count.ShouldBe(3);
        roundTripped.Certificates.ShouldContain(c => c.Use == KeyUse.Signing && c.Certificate.Thumbprint == cert.Thumbprint);
        roundTripped.Certificates.ShouldContain(c => c.Use == KeyUse.Encryption && c.Certificate.Thumbprint == cert.Thumbprint);
        roundTripped.Certificates.ShouldContain(c => c.Use == KeyUse.Both && c.Certificate.Thumbprint == cert.Thumbprint);
    }

    [Fact]
    public void AcsUrls_RoundTrip()
    {
        var model = new Models.SamlServiceProvider
        {
            EntityId = "https://sp.example.com",
            DisplayName = "Test SP",
            AssertionConsumerServiceUrls = new HashSet<IndexedEndpoint>
            {
                new IndexedEndpoint { Location = "https://sp.example.com/acs", Binding = SamlBinding.HttpPost },
                new IndexedEndpoint { Location = "https://sp.example.com/acs2", Binding = SamlBinding.HttpPost }
            }
        };

        var entity = model.ToEntity();
        entity.AssertionConsumerServiceUrls.Count.ShouldBe(2);

        var roundTripped = entity.ToModel();
        roundTripped.AssertionConsumerServiceUrls.Count.ShouldBe(2);
        roundTripped.AssertionConsumerServiceUrls.ShouldContain(u => u.Location == "https://sp.example.com/acs");
        roundTripped.AssertionConsumerServiceUrls.ShouldContain(u => u.Location == "https://sp.example.com/acs2");
    }

    [Fact]
    public void SingleLogoutServiceUrl_RoundTrip()
    {
        var model = new Models.SamlServiceProvider
        {
            EntityId = "https://sp.example.com",
            DisplayName = "Test SP",
            SingleLogoutServiceUrls = [new SamlEndpointType
            {
                Location = "https://sp.example.com/slo",
                Binding = SamlBinding.HttpPost
            }]
        };

        var entity = model.ToEntity();
        entity.SingleLogoutServiceUrls.ShouldHaveSingleItem();
        entity.SingleLogoutServiceUrls[0].Location.ShouldBe("https://sp.example.com/slo");
        entity.SingleLogoutServiceUrls[0].Binding.ShouldBe("urn:oasis:names:tc:SAML:2.0:bindings:HTTP-POST");

        var roundTripped = entity.ToModel();
        roundTripped.SingleLogoutServiceUrls.ShouldHaveSingleItem();
        var slo = roundTripped.SingleLogoutServiceUrls.First();
        slo.Location.ShouldBe("https://sp.example.com/slo");
        slo.Binding.ShouldBe(SamlBinding.HttpPost);
    }

    [Fact]
    public void TimeSpan_RoundTrip()
    {
        var clockSkew = TimeSpan.FromSeconds(30);
        var requestMaxAge = TimeSpan.FromMinutes(5);

        var model = new Models.SamlServiceProvider
        {
            EntityId = "https://sp.example.com",
            DisplayName = "Test SP",
            ClockSkew = clockSkew,
            RequestMaxAge = requestMaxAge
        };

        var entity = model.ToEntity();
        entity.ClockSkewSeconds.ShouldBe(30.0);
        entity.RequestMaxAgeSeconds.ShouldBe(300.0);

        var roundTripped = entity.ToModel();
        roundTripped.ClockSkew.ShouldBe(clockSkew);
        roundTripped.RequestMaxAge.ShouldBe(requestMaxAge);
    }

    [Fact]
    public void Mapping_Model_To_Entity_Maps_All_Properties()
    {
        var notMapped = new string[]
        {
            "Id",
            "Created",
            "Updated",
            "LastAccessed",
            "NonEditable",
            "EncryptAssertions",
            "RequireConsent",
        };

        var notAutoInitialized = new string[]
        {
            "AssertionConsumerServiceUrls",
            "Certificates",
            "SingleLogoutServiceUrls",
            "ClaimMappings",
            "AuthnContextMappings",
            "AllowedScopes",
            "RequestedClaimTypes",
        };

        MapperTestHelpers
            .AllPropertiesAreMapped<Models.SamlServiceProvider, Entities.SamlServiceProvider>(
                notAutoInitialized,
                source =>
                {
                    source.AssertionConsumerServiceUrls = new HashSet<IndexedEndpoint> { new IndexedEndpoint { Location = "https://sp.example.com/acs", Binding = SamlBinding.HttpPost } };
                    source.Certificates = new List<ServiceProviderCertificate>
                    {
                        new ServiceProviderCertificate { Certificate = MapperTestHelpers.CreateTestCertificate(), Use = KeyUse.Signing },
                        new ServiceProviderCertificate { Certificate = MapperTestHelpers.CreateTestCertificate(), Use = KeyUse.Both }
                    };
                    source.SingleLogoutServiceUrls = new HashSet<SamlEndpointType> { new SamlEndpointType
                    {
                        Location = "https://sp.example.com/slo",
                        Binding = SamlBinding.HttpPost
                    } };
                    source.ClaimMappings = new Dictionary<string, string> { { "key", "value" } };
                    source.AuthnContextMappings = new Dictionary<string, string> { { "pwd", "urn:oasis:names:tc:SAML:2.0:ac:classes:Password" } };
                    source.AllowedScopes = new HashSet<string> { "openid", "profile" };
                    source.RequestedClaimTypes = ["email", "name"];
                },
                source => source.ToEntity(),
                notMapped,
                out var unmappedMembers)
            .ShouldBeTrue($"{string.Join(',', unmappedMembers)} should be mapped");
    }

    [Fact]
    public void Mapping_Entity_To_Model_Maps_All_Properties()
    {
        var notMapped = new string[]
        {
            // IConnectedApplication explicit interface members — not public properties on the model
            // Obsolete properties from legacy model — not mapped from EF entity
            "AssertionConsumerServiceUrlsOld",
            "AssertionConsumerServiceBinding",
        };

        var notAutoInitialized = new string[]
        {
            "AssertionConsumerServiceUrls",
            "Certificates",
            "SingleLogoutServiceUrls",
            "ClaimMappings",
            "AuthnContextMappings",
            "AllowedScopes",
            "RequestedClaimTypes",
            "ClockSkewSeconds",
            "RequestMaxAgeSeconds",
            "AssertionLifetimeSeconds",
        };

        MapperTestHelpers
            .AllPropertiesAreMapped<Entities.SamlServiceProvider, Models.SamlServiceProvider>(
                notAutoInitialized,
                source =>
                {
                    source.AssertionConsumerServiceUrls = new List<Entities.SamlAssertionConsumerService>
                    {
                        new Entities.SamlAssertionConsumerService { Location = "https://sp.example.com/acs", Binding = SamlBinding.HttpPost.ToUrn() }
                    };
                    source.Certificates = new List<Entities.SamlCertificate>
                    {
                        new Entities.SamlCertificate
                        {
                            Data = Convert.ToBase64String(MapperTestHelpers.CreateTestCertificate().Export(X509ContentType.Cert)),
                            Use = (int)KeyUse.Signing
                        },
                        new Entities.SamlCertificate
                        {
                            Data = Convert.ToBase64String(MapperTestHelpers.CreateTestCertificate().Export(X509ContentType.Cert)),
                            Use = (int)KeyUse.Both
                        }
                    };
                    source.SingleLogoutServiceUrls = new List<Entities.SamlSingleLogoutService>
                    {
                        new Entities.SamlSingleLogoutService { Location = "https://sp.example.com/slo", Binding = "urn:oasis:names:tc:SAML:2.0:bindings:HTTP-POST" }
                    };
                    source.ClaimMappings = new List<Entities.SamlClaimMapping>
                    {
                        new Entities.SamlClaimMapping { ClaimType = "key", SamlAttributeName = "value" }
                    };
                    source.AuthnContextMappings = new List<Entities.SamlAuthnContextMapping>
                    {
                        new Entities.SamlAuthnContextMapping { OidcValue = "pwd", SamlAuthnContextClassRef = "urn:oasis:names:tc:SAML:2.0:ac:classes:Password" }
                    };
                    source.AllowedScopes = new List<Entities.SamlAllowedScope>
                    {
                        new Entities.SamlAllowedScope { Scope = "openid" },
                        new Entities.SamlAllowedScope { Scope = "profile" }
                    };
                    source.RequestedClaimTypes = new List<Entities.SamlRequestedClaimType>
                    {
                        new Entities.SamlRequestedClaimType { ClaimType = "email" },
                        new Entities.SamlRequestedClaimType { ClaimType = "name" }
                    };
                    source.ClockSkewSeconds = 30.0;
                    source.RequestMaxAgeSeconds = 300.0;
                    source.AssertionLifetimeSeconds = 600.0;
                },
                source => source.ToModel(),
                notMapped,
                out var unmappedMembers)
            .ShouldBeTrue($"{string.Join(',', unmappedMembers)} should be mapped");
    }
}
