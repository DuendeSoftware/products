// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using System.Collections.ObjectModel;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Xml;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Saml;
using Duende.IdentityServer.Saml.Bindings;
using Duende.IdentityServer.Saml.Models;
using Duende.IdentityServer.Saml.ResponseHandling;
using Duende.IdentityServer.Saml.Samlp;
using Duende.IdentityServer.Saml.Serialization;
using Duende.IdentityServer.Saml.Services;
using Duende.IdentityServer.Saml.Services.Default;
using Duende.IdentityServer.Saml.Validation;
using Duende.IdentityServer.Services;
using Microsoft.Extensions.Options;
using UnitTests.Common;
using NameIdPolicy = Duende.IdentityServer.Saml.Samlp.NameIdPolicy;
using SamlAttribute = Duende.IdentityServer.Saml.SamlAttribute;

namespace UnitTests.Saml;

public sealed class Saml2SsoResponseGeneratorTests
{
    private const string Category = "Saml2SsoResponseGenerator";

    private readonly Ct _ct = TestContext.Current.CancellationToken;

    private static SamlServiceProvider BuildSp(IDictionary<string, string>? claimMappings = null)
        => new()
        {
            EntityId = "https://sp.example.com",
            AssertionConsumerServiceUrls = [new IndexedEndpoint { Location = "https://sp.example.com/acs", Binding = SamlBinding.HttpPost }],
            ClaimMappings = claimMappings ?? new Dictionary<string, string>()
        };

    private static ValidatedAuthnRequest BuildRequest(SamlServiceProvider sp)
        => BuildRequest(sp, nameIdPolicy: null);

    private static ValidatedAuthnRequest BuildRequest(SamlServiceProvider sp, NameIdPolicy? nameIdPolicy)
        => BuildRequest(sp, nameIdPolicy, [new Claim("sub", "user1")]);

    private static ValidatedAuthnRequest BuildRequest(
        SamlServiceProvider sp,
        NameIdPolicy? nameIdPolicy,
        Claim[] claims)
        => new()
        {
            IdentityServerOptions = new IdentityServerOptions(),
            AuthnRequest = new AuthnRequest
            {
                Issuer = new NameId("https://sp.example.com"),
                NameIdPolicy = nameIdPolicy
            },
            Binding = SamlConstants.Bindings.HttpPost,
            Saml2Message = new InboundSaml2Message
            {
                Name = SamlConstants.RequestProperties.SAMLRequest,
                Xml = new XmlDocument().CreateElement("SAMLRequest"),
                Destination = "https://idp.example.com/saml/sso",
                Binding = SamlConstants.Bindings.HttpPost,
            },
            Saml2Sp = sp,
            Saml2IdpEntityId = "https://idp.example.com",
            AssertionConsumerService = new IndexedEndpoint { Location = "https://sp.example.com/acs", Binding = SamlBinding.HttpPost },
            Subject = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")),
            NameIdPolicyFormat = nameIdPolicy?.Format
        };

    private static Saml2SSoResponseGenerator CreateGenerator(
        SamlOptions? options = null,
        IProfileService? profileService = null,
        ISamlNameIdGenerator? nameIdGenerator = null)
    {
        var opts = options ?? new SamlOptions();
        var idServerOptions = Options.Create(new IdentityServerOptions { Saml = opts });
        return new(
            new StubSaml2IssuerNameService(IdpEntityId),
            TimeProvider.System,
            new SamlXmlWriter(),
            profileService ?? new MockProfileService(),
            new MockSamlSigningService(TestCert.Load()),
            idServerOptions,
            nameIdGenerator ?? new DefaultSamlNameIdGenerator(idServerOptions));
    }

    private static (string Value, string? Format, string? SPNameQualifier) GetNameId(XmlElement xml)
    {
        var nsMgr = new XmlNamespaceManager(xml.OwnerDocument.NameTable);
        nsMgr.AddNamespace("saml", SamlConstants.Namespaces.Assertion);

        var nameId = (XmlElement)xml.SelectSingleNode("//saml:Subject/saml:NameID", nsMgr)!;
        return (nameId.InnerText, nameId.GetAttribute("Format"), nameId.GetAttribute("SPNameQualifier"));
    }

    private static List<(string Name, List<string> Values)> GetAttributes(XmlElement xml)
    {
        var nsMgr = new XmlNamespaceManager(xml.OwnerDocument.NameTable);
        nsMgr.AddNamespace("saml", SamlConstants.Namespaces.Assertion);

        var attributes = new List<(string Name, List<string> Values)>();
        foreach (XmlElement attr in xml.SelectNodes("//saml:Attribute", nsMgr)!)
        {
            var name = attr.GetAttribute("Name");
            var values = new List<string>();
            foreach (XmlElement val in attr.SelectNodes("saml:AttributeValue", nsMgr)!)
            {
                values.Add(val.InnerText);
            }
            attributes.Add((name, values));
        }
        return attributes;
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task SpMappingsUsedWhenSpHasNonEmptyClaimMappings()
    {
        var profileService = new MockProfileService { ProfileClaims = [new Claim("email", "user@example.com")] };
        var generator = CreateGenerator(profileService: profileService);
        var sp = BuildSp(new Dictionary<string, string> { ["email"] = "sp:email" });

        var result = await generator.CreateResponse(BuildRequest(sp), _ct);

        var attrs = GetAttributes(result.Message!.Xml);
        attrs.ShouldContain(a => a.Name == "sp:email" && a.Values.Contains("user@example.com"));
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task SpMappingsReplaceGlobalMappingsEntirely()
    {
        var options = new SamlOptions
        {
            DefaultClaimMappings = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>
            {
                ["name"] = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name"
            })
        };
        var profileService = new MockProfileService
        {
            ProfileClaims =
            [
                new Claim("email", "user@example.com"),
                new Claim("name", "Jane")
            ]
        };
        var generator = CreateGenerator(options, profileService);
        var sp = BuildSp(new Dictionary<string, string> { ["email"] = "sp:email" });

        var result = await generator.CreateResponse(BuildRequest(sp), _ct);

        var attrs = GetAttributes(result.Message!.Xml);
        attrs.ShouldContain(a => a.Name == "sp:email");
        attrs.ShouldNotContain(a => a.Name == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name");
        attrs.ShouldContain(a => a.Name == "name" && a.Values.Contains("Jane"));
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task GlobalDefaultClaimMappingsUsedWhenSpHasEmptyClaimMappings()
    {
        var options = new SamlOptions
        {
            DefaultClaimMappings = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>
            {
                ["name"] = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name"
            })
        };
        var profileService = new MockProfileService { ProfileClaims = [new Claim("name", "Jane Doe")] };
        var generator = CreateGenerator(options, profileService);

        var result = await generator.CreateResponse(BuildRequest(BuildSp()), _ct);

        var attrs = GetAttributes(result.Message!.Xml);
        attrs.ShouldContain(a =>
            a.Name == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name" &&
            a.Values.Contains("Jane Doe"));
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task UnmappedClaimsPassThroughWithClaimTypeAsAttributeName()
    {
        var profileService = new MockProfileService { ProfileClaims = [new Claim("custom:claim", "value1")] };
        var generator = CreateGenerator(profileService: profileService);

        var result = await generator.CreateResponse(BuildRequest(BuildSp()), _ct);

        var attrs = GetAttributes(result.Message!.Xml!);
        attrs.ShouldContain(a => a.Name == "custom:claim" && a.Values.Contains("value1"));
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task MultiValuedClaimsWithSameTypeGroupedIntoSingleAttribute()
    {
        var profileService = new MockProfileService
        {
            ProfileClaims =
            [
                new Claim("department", "Engineering"),
                new Claim("department", "Operations"),
                new Claim("department", "Finance")
            ]
        };
        var generator = CreateGenerator(profileService: profileService);

        var result = await generator.CreateResponse(BuildRequest(BuildSp()), _ct);

        var attrs = GetAttributes(result.Message!.Xml);
        var dept = attrs.Where(a => a.Name == "department").ToList();
        dept.Count.ShouldBe(1);
        dept[0].Values.Count.ShouldBe(3);
        dept[0].Values.ShouldContain("Engineering");
        dept[0].Values.ShouldContain("Operations");
        dept[0].Values.ShouldContain("Finance");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task TwoDifferentClaimTypesMappedToSameAttributeNameAreGrouped()
    {
        var profileService = new MockProfileService
        {
            ProfileClaims =
            [
                new Claim("email", "a@example.com"),
                new Claim("mail", "b@example.com")
            ]
        };
        var generator = CreateGenerator(profileService: profileService);
        var sp = BuildSp(new Dictionary<string, string>
        {
            ["email"] = "contact",
            ["mail"] = "contact"
        });

        var result = await generator.CreateResponse(BuildRequest(sp), _ct);

        var attrs = GetAttributes(result.Message!.Xml);
        var contact = attrs.Where(a => a.Name == "contact").ToList();
        contact.Count.ShouldBe(1);
        contact[0].Values.Count.ShouldBe(2);
        contact[0].Values.ShouldContain("a@example.com");
        contact[0].Values.ShouldContain("b@example.com");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task NoClaimsProducesNoAttributeElements()
    {
        var generator = CreateGenerator();

        var result = await generator.CreateResponse(BuildRequest(BuildSp()), _ct);

        var attrs = GetAttributes(result.Message!.Xml);
        attrs.ShouldBeEmpty();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task MixOfMappedAndUnmappedClaims()
    {
        var options = new SamlOptions
        {
            DefaultClaimMappings = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>
            {
                ["name"] = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name"
            })
        };
        var profileService = new MockProfileService
        {
            ProfileClaims =
            [
                new Claim("name", "Jane"),
                new Claim("department", "Engineering")
            ]
        };
        var generator = CreateGenerator(options, profileService);

        var result = await generator.CreateResponse(BuildRequest(BuildSp()), _ct);

        var attrs = GetAttributes(result.Message!.Xml);
        attrs.ShouldContain(a => a.Name == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name");
        attrs.ShouldContain(a => a.Name == "department");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task SamlAttributesFromProfileServiceAreIncluded()
    {
        var profileService = new MockProfileService();
        profileService.SamlAttributesToReturn.Add(new SamlAttribute { Name = "direct-attr", Values = ["val1"] });
        var generator = CreateGenerator(profileService: profileService);

        var result = await generator.CreateResponse(BuildRequest(BuildSp()), _ct);

        var attrs = GetAttributes(result.Message!.Xml);
        attrs.ShouldContain(a => a.Name == "direct-attr" && a.Values.Contains("val1"));
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task SamlAttributesAndMappedClaimsAreMerged()
    {
        var profileService = new MockProfileService();
        profileService.SamlAttributesToReturn.Add(new SamlAttribute { Name = "direct-attr", Values = ["dval"] });
        profileService.ProfileClaims = [new Claim("role", "Admin")];
        var generator = CreateGenerator(profileService: profileService);
        var sp = BuildSp(new Dictionary<string, string> { ["role"] = "saml:role" });

        var result = await generator.CreateResponse(BuildRequest(sp), _ct);

        var attrs = GetAttributes(result.Message!.Xml);
        attrs.ShouldContain(a => a.Name == "direct-attr");
        attrs.ShouldContain(a => a.Name == "saml:role");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task DuplicateAttributeNamesFromSamlAttributesAndMappedClaimsAreMerged()
    {
        var profileService = new MockProfileService();
        profileService.SamlAttributesToReturn.Add(new SamlAttribute { Name = "saml:role", Values = ["Reader"] });
        profileService.ProfileClaims = [new Claim("role", "Admin")];
        var generator = CreateGenerator(profileService: profileService);
        var sp = BuildSp(new Dictionary<string, string> { ["role"] = "saml:role" });

        var result = await generator.CreateResponse(BuildRequest(sp), _ct);

        var attrs = GetAttributes(result.Message!.Xml);
        var roles = attrs.Where(a => a.Name == "saml:role").ToList();
        roles.Count.ShouldBe(1);
        roles[0].Values.ShouldContain("Reader");
        roles[0].Values.ShouldContain("Admin");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task RequestedClaimTypesFromSpConfigPassedToProfileService()
    {
        var profileService = new MockProfileService();
        var generator = CreateGenerator(profileService: profileService);
        var sp = BuildSp();
        sp.RequestedClaimTypes = ["email", "name", "role"];
        var request = BuildRequest(sp);
        request.RequestedClaimTypes = sp.RequestedClaimTypes;

        await generator.CreateResponse(request, _ct);

        profileService.ProfileContext.ShouldNotBeNull();
        profileService.ProfileContext.RequestedClaimTypes.ShouldBe(sp.RequestedClaimTypes);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task DefaultNameIdUsesSubClaimWithUnspecifiedFormat()
    {
        var generator = CreateGenerator();
        var sp = BuildSp();

        var result = await generator.CreateResponse(BuildRequest(sp), _ct);

        var (value, format, _) = GetNameId(result.Message!.Xml);
        value.ShouldBe("user1");
        format.ShouldBe(SamlConstants.NameIdentifierFormats.Unspecified);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task UnspecifiedFormatUsedWhenNoPolicyAndNoSpDefault()
    {
        var generator = CreateGenerator();
        var sp = BuildSp();
        sp.DefaultNameIdFormat = null;

        var result = await generator.CreateResponse(BuildRequest(sp), _ct);

        var (value, format, _) = GetNameId(result.Message!.Xml);
        value.ShouldBe("user1");
        format.ShouldBe(SamlConstants.NameIdentifierFormats.Unspecified);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task EmailFormatUsesEmailClaim()
    {
        var generator = CreateGenerator();
        var sp = BuildSp();
        var policy = new NameIdPolicy { Format = SamlConstants.NameIdentifierFormats.EmailAddress };
        var claims = new[] { new Claim("sub", "user1"), new Claim("email", "user@example.com") };

        var result = await generator.CreateResponse(BuildRequest(sp, policy, claims), _ct);

        var (value, format, _) = GetNameId(result.Message!.Xml);
        value.ShouldBe("user@example.com");
        format.ShouldBe(SamlConstants.NameIdentifierFormats.EmailAddress);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task EmailFormatReturnsErrorWhenEmailClaimMissing()
    {
        var generator = CreateGenerator();
        var sp = BuildSp();
        var policy = new NameIdPolicy { Format = SamlConstants.NameIdentifierFormats.EmailAddress };
        var claims = new[] { new Claim("sub", "user1") };

        var result = await generator.CreateResponse(BuildRequest(sp, policy, claims), _ct);

        result.Error.ShouldNotBeNull();
        result.Message.ShouldBeNull();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task EmailFormatUsesCustomGlobalClaimType()
    {
        var options = new SamlOptions { EmailNameIdClaimType = "mail" };
        var generator = CreateGenerator(options);
        var sp = BuildSp();
        var policy = new NameIdPolicy { Format = SamlConstants.NameIdentifierFormats.EmailAddress };
        var claims = new[] { new Claim("sub", "user1"), new Claim("mail", "user@corp.com") };

        var result = await generator.CreateResponse(BuildRequest(sp, policy, claims), _ct);

        var (value, format, _) = GetNameId(result.Message!.Xml);
        value.ShouldBe("user@corp.com");
        format.ShouldBe(SamlConstants.NameIdentifierFormats.EmailAddress);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task EmailFormatUsesSpClaimTypeOverride()
    {
        var generator = CreateGenerator();
        var sp = BuildSp();
        sp.EmailNameIdClaimType = "preferred_email";
        var policy = new NameIdPolicy { Format = SamlConstants.NameIdentifierFormats.EmailAddress };
        var claims = new[] { new Claim("sub", "user1"), new Claim("preferred_email", "sp@example.com") };

        var result = await generator.CreateResponse(BuildRequest(sp, policy, claims), _ct);

        var (value, format, _) = GetNameId(result.Message!.Xml);
        value.ShouldBe("sp@example.com");
        format.ShouldBe(SamlConstants.NameIdentifierFormats.EmailAddress);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task RequestNameIdPolicyFormatTakesPriorityOverSpDefault()
    {
        var generator = CreateGenerator();
        var sp = BuildSp();
        sp.DefaultNameIdFormat = SamlConstants.NameIdentifierFormats.Unspecified;
        var policy = new NameIdPolicy { Format = SamlConstants.NameIdentifierFormats.EmailAddress };
        var claims = new[] { new Claim("sub", "user1"), new Claim("email", "user@example.com") };

        var result = await generator.CreateResponse(BuildRequest(sp, policy, claims), _ct);

        var (_, format, _) = GetNameId(result.Message!.Xml);
        format.ShouldBe(SamlConstants.NameIdentifierFormats.EmailAddress);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task SpDefaultFormatUsedWhenNoNameIdPolicy()
    {
        var generator = CreateGenerator();
        var sp = BuildSp();
        sp.DefaultNameIdFormat = SamlConstants.NameIdentifierFormats.EmailAddress;
        var claims = new[] { new Claim("sub", "user1"), new Claim("email", "sp@example.com") };

        var result = await generator.CreateResponse(BuildRequest(sp, null, claims), _ct);

        var (_, format, _) = GetNameId(result.Message!.Xml);
        format.ShouldBe(SamlConstants.NameIdentifierFormats.EmailAddress);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task CustomNameIdGeneratorOutputAppearsInResponse()
    {
        var customGenerator = new CustomNameIdGenerator("custom-id-value", SamlConstants.NameIdentifierFormats.EmailAddress);
        var generator = CreateGenerator(nameIdGenerator: customGenerator);
        var sp = BuildSp();

        var result = await generator.CreateResponse(BuildRequest(sp), _ct);

        var (value, format, _) = GetNameId(result.Message!.Xml);
        value.ShouldBe("custom-id-value");
        format.ShouldBe(SamlConstants.NameIdentifierFormats.EmailAddress);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task SessionIndexIncludedInAuthnStatementWhenProvided()
    {
        var generator = CreateGenerator();
        var sp = BuildSp();
        var request = BuildRequest(sp);
        request = new ValidatedAuthnRequest
        {
            IdentityServerOptions = request.IdentityServerOptions,
            AuthnRequest = request.AuthnRequest,
            Binding = request.Binding,
            Saml2Message = request.Saml2Message,
            Saml2Sp = request.Saml2Sp,
            Saml2IdpEntityId = request.Saml2IdpEntityId,
            AssertionConsumerService = request.AssertionConsumerService,
            Subject = request.Subject,
            SessionIndex = "idx_abc123"
        };

        var result = await generator.CreateResponse(request, _ct);

        var nsMgr = new XmlNamespaceManager(result.Message!.Xml.OwnerDocument.NameTable);
        nsMgr.AddNamespace("saml", SamlConstants.Namespaces.Assertion);
        var authnStatement = (XmlElement)result.Message.Xml.SelectSingleNode("//saml:AuthnStatement", nsMgr)!;
        authnStatement.GetAttribute("SessionIndex").ShouldBe("idx_abc123");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task SessionIndexOmittedFromAuthnStatementWhenNull()
    {
        var generator = CreateGenerator();
        var sp = BuildSp();
        var request = BuildRequest(sp);

        var result = await generator.CreateResponse(request, _ct);

        var nsMgr = new XmlNamespaceManager(result.Message!.Xml.OwnerDocument.NameTable);
        nsMgr.AddNamespace("saml", SamlConstants.Namespaces.Assertion);
        var authnStatement = (XmlElement)result.Message.Xml.SelectSingleNode("//saml:AuthnStatement", nsMgr)!;
        authnStatement.HasAttribute("SessionIndex").ShouldBeFalse();
    }

    private sealed class CustomNameIdGenerator(string nameIdValue, string format) : ISamlNameIdGenerator
    {
        public Task<NameIdGenerationResult> GenerateAsync(NameIdGenerationContext context, Ct ct)
            => Task.FromResult(NameIdGenerationResult.Success(new NameId { Value = nameIdValue, Format = format }));
    }

    private static string GetAuthnContextClassRef(XmlElement xml)
    {
        var nsMgr = new XmlNamespaceManager(xml.OwnerDocument.NameTable);
        nsMgr.AddNamespace("saml", SamlConstants.Namespaces.Assertion);
        var node = xml.SelectSingleNode("//saml:AuthnStatement/saml:AuthnContext/saml:AuthnContextClassRef", nsMgr);
        return node!.InnerText;
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task DefaultAmrPwdMapsToPasswordProtectedTransport()
    {
        var generator = CreateGenerator();
        var sp = BuildSp();
        var claims = new[] { new Claim("sub", "user1"), new Claim("amr", "pwd") };

        var result = await generator.CreateResponse(BuildRequest(sp, null, claims), _ct);

        GetAuthnContextClassRef(result.Message!.Xml)
            .ShouldBe(SamlConstants.AuthnContextClasses.PasswordProtectedTransport);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task DefaultAmrExternalMapsToUnspecified()
    {
        var generator = CreateGenerator();
        var sp = BuildSp();
        var claims = new[] { new Claim("sub", "user1"), new Claim("amr", "external") };

        var result = await generator.CreateResponse(BuildRequest(sp, null, claims), _ct);

        GetAuthnContextClassRef(result.Message!.Xml)
            .ShouldBe(SamlConstants.AuthnContextClasses.Unspecified);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task AcrClaimTakesPriorityOverAmr()
    {
        var options = new SamlOptions
        {
            DefaultAuthnContextMappings = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>
            {
                ["urn:mfa"] = SamlConstants.AuthnContextClasses.PasswordProtectedTransport,
                ["pwd"] = SamlConstants.AuthnContextClasses.Unspecified
            })
        };
        var generator = CreateGenerator(options);
        var sp = BuildSp();
        var claims = new[]
        {
            new Claim("sub", "user1"),
            new Claim("acr", "urn:mfa"),
            new Claim("amr", "pwd")
        };

        var result = await generator.CreateResponse(BuildRequest(sp, null, claims), _ct);

        GetAuthnContextClassRef(result.Message!.Xml)
            .ShouldBe(SamlConstants.AuthnContextClasses.PasswordProtectedTransport);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task UnmappedAcrFallsThroughToAmr()
    {
        var generator = CreateGenerator();
        var sp = BuildSp();
        var claims = new[]
        {
            new Claim("sub", "user1"),
            new Claim("acr", "urn:some:unknown:level"),
            new Claim("amr", "pwd")
        };

        var result = await generator.CreateResponse(BuildRequest(sp, null, claims), _ct);

        GetAuthnContextClassRef(result.Message!.Xml)
            .ShouldBe(SamlConstants.AuthnContextClasses.PasswordProtectedTransport);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task NoMatchingClaimsFallsBackToUnspecified()
    {
        var options = new SamlOptions
        {
            DefaultAuthnContextMappings = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>
            {
                ["mfa"] = SamlConstants.AuthnContextClasses.PasswordProtectedTransport
            })
        };
        var generator = CreateGenerator(options);
        var sp = BuildSp();
        var claims = new[] { new Claim("sub", "user1") };

        var result = await generator.CreateResponse(BuildRequest(sp, null, claims), _ct);

        GetAuthnContextClassRef(result.Message!.Xml)
            .ShouldBe(SamlConstants.AuthnContextClasses.Unspecified);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task SpAuthnContextMappingsOverrideGlobalMappings()
    {
        var options = new SamlOptions
        {
            DefaultAuthnContextMappings = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>
            {
                ["pwd"] = SamlConstants.AuthnContextClasses.Unspecified
            })
        };
        var generator = CreateGenerator(options);
        var sp = BuildSp();
        sp.AuthnContextMappings = new Dictionary<string, string>
        {
            ["pwd"] = SamlConstants.AuthnContextClasses.PasswordProtectedTransport
        };
        var claims = new[] { new Claim("sub", "user1"), new Claim("amr", "pwd") };

        var result = await generator.CreateResponse(BuildRequest(sp, null, claims), _ct);

        GetAuthnContextClassRef(result.Message!.Xml)
            .ShouldBe(SamlConstants.AuthnContextClasses.PasswordProtectedTransport);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task MultiValuedAmrUsesFirstMatch()
    {
        var options = new SamlOptions
        {
            DefaultAuthnContextMappings = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>
            {
                ["otp"] = SamlConstants.AuthnContextClasses.PasswordProtectedTransport
            })
        };
        var generator = CreateGenerator(options);
        var sp = BuildSp();
        var claims = new[]
        {
            new Claim("sub", "user1"),
            new Claim("amr", "pwd"),
            new Claim("amr", "otp")
        };

        var result = await generator.CreateResponse(BuildRequest(sp, null, claims), _ct);

        GetAuthnContextClassRef(result.Message!.Xml)
            .ShouldBe(SamlConstants.AuthnContextClasses.PasswordProtectedTransport);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task CustomGlobalMappingUsedWhenSpHasEmptyMappings()
    {
        var options = new SamlOptions
        {
            DefaultAuthnContextMappings = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>
            {
                ["pwd"] = "urn:oasis:names:tc:SAML:2.0:ac:classes:Password"
            })
        };
        var generator = CreateGenerator(options);
        var sp = BuildSp();
        var claims = new[] { new Claim("sub", "user1"), new Claim("amr", "pwd") };

        var result = await generator.CreateResponse(BuildRequest(sp, null, claims), _ct);

        GetAuthnContextClassRef(result.Message!.Xml)
            .ShouldBe("urn:oasis:names:tc:SAML:2.0:ac:classes:Password");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task AcrValueAlreadyAUriMappedThroughDictionary()
    {
        var options = new SamlOptions
        {
            DefaultAuthnContextMappings = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>
            {
                ["urn:oasis:names:tc:SAML:2.0:ac:classes:PasswordProtectedTransport"] = SamlConstants.AuthnContextClasses.PasswordProtectedTransport
            })
        };
        var generator = CreateGenerator(options);
        var sp = BuildSp();
        var claims = new[]
        {
            new Claim("sub", "user1"),
            new Claim("acr", "urn:oasis:names:tc:SAML:2.0:ac:classes:PasswordProtectedTransport")
        };

        var result = await generator.CreateResponse(BuildRequest(sp, null, claims), _ct);

        GetAuthnContextClassRef(result.Message!.Xml)
            .ShouldBe(SamlConstants.AuthnContextClasses.PasswordProtectedTransport);
    }

    // --- Error response tests ---

    private const string IdpEntityId = "https://idp.example.com";
    private const string SpEntityId = "https://sp.example.com";
    private const string AcsUrl = "https://sp.example.com/acs";
    private const string RequestId = "_requestid123";
    private const string RelayStateValue = "some-relay-state";

    private static Saml2SSoResponseGenerator CreateErrorTestGenerator(
        string? issuer = null,
        SamlSigningBehavior signingBehavior = SamlSigningBehavior.DoNotSign,
        X509Certificate2? signingCertificate = null)
    {
        var samlOptions = new SamlOptions { DefaultSigningBehavior = signingBehavior };
        var idServerOptions = Options.Create(new IdentityServerOptions { Saml = samlOptions });
        return new(
            new StubSaml2IssuerNameService(issuer ?? IdpEntityId),
            new FakeTimeProvider(),
            new SamlXmlWriter(),
            new MockProfileService(),
            new StubSamlSigningService(signingCertificate),
            idServerOptions,
            new DefaultSamlNameIdGenerator(idServerOptions));
    }

    private static ValidatedAuthnRequest CreateErrorTestRequest(
        string? relayState = null,
        SamlSigningBehavior? spSigningBehavior = null)
    {
        var sp = new SamlServiceProvider
        {
            EntityId = SpEntityId,
            AssertionConsumerServiceUrls = [new IndexedEndpoint { Location = AcsUrl, IsDefault = true, Binding = SamlBinding.HttpPost }]
        };
        if (spSigningBehavior.HasValue)
        {
            sp.SigningBehavior = spSigningBehavior.Value;
        }

        return new ValidatedAuthnRequest
        {
            IdentityServerOptions = new IdentityServerOptions(),
            AuthnRequest = new AuthnRequest
            {
                Id = RequestId,
                Issuer = new NameId(SpEntityId)
            },
            Binding = SamlConstants.Bindings.HttpPost,
            Saml2Message = new InboundSaml2Message
            {
                Name = "SAMLRequest",
                Xml = new XmlDocument().CreateElement("SAMLRequest"),
                Destination = "https://idp.example.com/Saml2/SSO",
                Binding = SamlConstants.Bindings.HttpPost
            },
            Saml2IdpEntityId = IdpEntityId,
            RelayState = relayState,
            Saml2Sp = sp,
            AssertionConsumerService = new IndexedEndpoint { Location = AcsUrl, IsDefault = true, Binding = SamlBinding.HttpPost },
            Subject = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "user1")], "test")),
            RequestId = RequestId
        };
    }

    private static Saml2InteractionResponse NoPassiveError() =>
        Saml2InteractionResponse.Error(SamlStatusCodes.Responder, SamlStatusCodes.NoPassive, "Cannot passively authenticate user");

    [Fact]
    [Trait("Category", Category)]
    public async Task CreateErrorResponseReturnsSamlErrorResponseViaBinding()
    {
        var generator = CreateErrorTestGenerator();
        var request = CreateErrorTestRequest();

        var result = await generator.CreateErrorResponse(request, NoPassiveError(), CancellationToken.None);

        result.Message.ShouldNotBeNull();
        result.Error.ShouldBeNull();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task CreateErrorResponseSetsCorrectDestination()
    {
        var generator = CreateErrorTestGenerator();
        var request = CreateErrorTestRequest();

        var result = await generator.CreateErrorResponse(request, NoPassiveError(), CancellationToken.None);

        result.Message!.Destination.ShouldBe(AcsUrl);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task CreateErrorResponseEchoesRelayState()
    {
        var generator = CreateErrorTestGenerator();
        var request = CreateErrorTestRequest(relayState: RelayStateValue);

        var result = await generator.CreateErrorResponse(request, NoPassiveError(), CancellationToken.None);

        result.Message!.RelayState.ShouldBe(RelayStateValue);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task CreateErrorResponseUsesHttpPostBinding()
    {
        var generator = CreateErrorTestGenerator();
        var request = CreateErrorTestRequest();

        var result = await generator.CreateErrorResponse(request, NoPassiveError(), CancellationToken.None);

        result.Message!.Binding.ShouldBe(SamlConstants.Bindings.HttpPost);
        result.Message.Name.ShouldBe(SamlConstants.RequestProperties.SAMLResponse);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task CreateErrorResponseXmlContainsResponderStatusCode()
    {
        var generator = CreateErrorTestGenerator();
        var request = CreateErrorTestRequest();

        var result = await generator.CreateErrorResponse(request, NoPassiveError(), CancellationToken.None);

        var xml = result.Message!.Xml.OuterXml;
        xml.ShouldContain(SamlStatusCodes.Responder);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task CreateErrorResponseXmlContainsNoPassiveSubStatusCode()
    {
        var generator = CreateErrorTestGenerator();
        var request = CreateErrorTestRequest();

        var result = await generator.CreateErrorResponse(request, NoPassiveError(), CancellationToken.None);

        var xml = result.Message!.Xml.OuterXml;
        xml.ShouldContain(SamlStatusCodes.NoPassive);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task CreateErrorResponseXmlContainsIssuer()
    {
        var generator = CreateErrorTestGenerator();
        var request = CreateErrorTestRequest();

        var result = await generator.CreateErrorResponse(request, NoPassiveError(), CancellationToken.None);

        var xml = result.Message!.Xml.OuterXml;
        xml.ShouldContain(IdpEntityId);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task CreateErrorResponseXmlContainsInResponseTo()
    {
        var generator = CreateErrorTestGenerator();
        var request = CreateErrorTestRequest();

        var result = await generator.CreateErrorResponse(request, NoPassiveError(), CancellationToken.None);

        var xml = result.Message!.Xml.OuterXml;
        xml.ShouldContain(RequestId);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task CreateErrorResponseXmlContainsDestination()
    {
        var generator = CreateErrorTestGenerator();
        var request = CreateErrorTestRequest();

        var result = await generator.CreateErrorResponse(request, NoPassiveError(), CancellationToken.None);

        var xml = result.Message!.Xml.OuterXml;
        xml.ShouldContain(AcsUrl);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task CreateErrorResponseXmlContainsNoAssertions()
    {
        var generator = CreateErrorTestGenerator();
        var request = CreateErrorTestRequest();

        var result = await generator.CreateErrorResponse(request, NoPassiveError(), CancellationToken.None);

        var xml = result.Message!.Xml.OuterXml;
        xml.ShouldNotContain("<saml:Assertion");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task CreateErrorResponseDoesNotSetSigningCertificateWhenSigningNotConfigured()
    {
        var generator = CreateErrorTestGenerator(signingBehavior: SamlSigningBehavior.DoNotSign);
        var request = CreateErrorTestRequest();

        var result = await generator.CreateErrorResponse(request, NoPassiveError(), CancellationToken.None);

        result.Message!.SigningCertificate.ShouldBeNull();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task CreateErrorResponseSetsSigningCertificateWhenResponseSigningConfigured()
    {
        using var rsa = RSA.Create(2048);
        var req = new CertificateRequest("cn=test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var cert = req.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
        var generator = CreateErrorTestGenerator(signingBehavior: SamlSigningBehavior.SignResponse, signingCertificate: cert);
        var request = CreateErrorTestRequest();

        var result = await generator.CreateErrorResponse(request, NoPassiveError(), CancellationToken.None);

        result.Message!.SigningCertificate.ShouldBe(cert);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task CreateErrorResponseReturnsErrorPageResultWhenIsSafeErrorReturnsFalse()
    {
        var samlOptions = new SamlOptions();
        var idServerOptions = Options.Create(new IdentityServerOptions { Saml = samlOptions });
        var generator = new AlwaysUnsafeResponseGenerator(
            new StubSaml2IssuerNameService(IdpEntityId),
            new FakeTimeProvider(),
            new SamlXmlWriter(),
            new MockProfileService(),
            new StubSamlSigningService(null),
            idServerOptions,
            new DefaultSamlNameIdGenerator(idServerOptions));
        var request = CreateErrorTestRequest();

        var result = await generator.CreateErrorResponse(request, NoPassiveError(), CancellationToken.None);

        result.Error.ShouldNotBeNull();
        result.Message.ShouldBeNull();
    }

    private sealed class AlwaysUnsafeResponseGenerator(
        ISaml2IssuerNameService issuerNameService,
        TimeProvider timeProvider,
        ISamlXmlWriter samlXmlWriter,
        IProfileService profileService,
        ISamlSigningService samlSigningService,
        IOptions<IdentityServerOptions> identityServerOptions,
        ISamlNameIdGenerator nameIdGenerator)
        : Saml2SSoResponseGenerator(issuerNameService, timeProvider, samlXmlWriter, profileService, samlSigningService, identityServerOptions, nameIdGenerator)
    {
        protected override bool IsSafeError(ValidatedAuthnRequest validatedAuthnRequest, Saml2InteractionResponse interactionResponse) =>
            false;
    }

    private sealed class StubSamlSigningService(X509Certificate2? certificate) : ISamlSigningService
    {
        public Task<X509Certificate2> GetSigningCertificateAsync(Ct ct) =>
            certificate != null
                ? Task.FromResult(certificate)
                : throw new InvalidOperationException("No signing certificate configured");

        public Task<string> GetSigningCertificateBase64Async(Ct ct) =>
            Task.FromResult(Convert.ToBase64String(certificate?.RawData ?? []));

        public Task<IReadOnlyList<X509Certificate2>> GetAllSigningCertificatesAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<X509Certificate2>>(certificate == null ? [] : [certificate]);
    }

    private sealed class FakeTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
    }
}
