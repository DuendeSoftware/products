// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Collections.ObjectModel;
using System.Security.Claims;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Internal.Saml;
using Duende.IdentityServer.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using UnitTests.Common;

namespace UnitTests.Saml;

public class SamlClaimsServiceTests
{
    private const string Category = "SAML Claims Service";

    private readonly Ct _ct = TestContext.Current.CancellationToken;

    private readonly SamlOptions _samlOptions;
    private readonly IOptions<SamlOptions> _options;
    private readonly MockProfileService _profileService;
    private readonly SamlClaimsService _service;

    public SamlClaimsServiceTests()
    {
        _samlOptions = new SamlOptions();
        _options = Options.Create(_samlOptions);
        _profileService = new MockProfileService();
        _service = new SamlClaimsService(_profileService, NullLogger<SamlClaimsService>.Instance, _options);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task default_mappings_should_map_common_oidc_claims()
    {
        // Arrange
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("name", "John Doe"),
            new Claim("email", "test@example.com"),
            new Claim("role", "Admin")
        }));

        var sp = new SamlServiceProvider
        {
            EntityId = "https://sp.example.com",
            DisplayName = "Test Service Provider",
            AssertionConsumerServiceUrls = new[] { new Uri("https://sp.example.com/acs") }
        };

        _profileService.ProfileClaims = user.Claims.ToList();

        // Act
        var attributes = (await _service.GetMappedAttributesAsync(user, sp, _ct)).ToList();

        // Assert
        attributes.Count.ShouldBe(3);

        var nameAttr = attributes.First(a => a.Name == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name");
        nameAttr.Values.Count.ShouldBe(1);
        nameAttr.Values[0].ShouldBe("John Doe");

        var emailAttr = attributes.First(a => a.Name == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress");
        emailAttr.Values.Count.ShouldBe(1);
        emailAttr.Values[0].ShouldBe("test@example.com");

        var roleAttr = attributes.First(a => a.Name == "http://schemas.xmlsoap.org/ws/2005/05/identity/role");
        roleAttr.Values.Count.ShouldBe(1);
        roleAttr.Values[0].ShouldBe("Admin");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task claim_types_constants_should_map_correctly()
    {
        // Arrange - use custom OID mappings for this test
        var customMappings = new Dictionary<string, string>
        {
            [ClaimTypes.NameIdentifier] = "urn:oid:0.9.2342.19200300.100.1.1",
            [ClaimTypes.Email] = "urn:oid:0.9.2342.19200300.100.1.3",
            [ClaimTypes.GivenName] = "urn:oid:2.5.4.42",
            [ClaimTypes.Surname] = "urn:oid:2.5.4.4"
        };
        var optionsWithOidMappings = new SamlOptions
        {
            DefaultClaimMappings = new ReadOnlyDictionary<string, string>(customMappings)
        };
        var service = new SamlClaimsService(_profileService, NullLogger<SamlClaimsService>.Instance, Options.Create(optionsWithOidMappings));

        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "user123"),
            new Claim(ClaimTypes.Email, "user@example.com"),
            new Claim(ClaimTypes.GivenName, "Jane"),
            new Claim(ClaimTypes.Surname, "Smith")
        }));

        var sp = new SamlServiceProvider
        {
            EntityId = "https://sp.example.com",
            DisplayName = "Test Service Provider",
            AssertionConsumerServiceUrls = new[] { new Uri("https://sp.example.com/acs") }
        };

        _profileService.ProfileClaims = user.Claims.ToList();

        // Act
        var attributes = (await service.GetMappedAttributesAsync(user, sp, _ct)).ToList();

        // Assert
        attributes.Count.ShouldBe(4);
        attributes.ShouldAllBe(a => a.Name.StartsWith("urn:oid:"));
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task cleared_default_mappings_should_exclude_unmapped_claims()
    {
        // Arrange
        var optionsWithNoMappings = new SamlOptions
        {
            DefaultClaimMappings = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>())
        };
        var service = new SamlClaimsService(_profileService, NullLogger<SamlClaimsService>.Instance, Options.Create(optionsWithNoMappings));

        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("sub", "test123"),
            new Claim("email", "test@example.com"),
            new Claim("custom_claim", "custom_value")
        }));

        var sp = new SamlServiceProvider
        {
            EntityId = "https://sp.example.com",
            DisplayName = "Test Service Provider",
            AssertionConsumerServiceUrls = new[] { new Uri("https://sp.example.com/acs") }
        };

        _profileService.ProfileClaims = user.Claims.ToList();

        // Act
        var attributes = (await service.GetMappedAttributesAsync(user, sp, _ct)).ToList();

        // Assert
        attributes.Count.ShouldBe(0); // No mappings, so no attributes
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task custom_global_mappings_should_apply_custom_mappings()
    {
        // Arrange
        var customMappings = new Dictionary<string, string>
        {
            ["email"] = "emailAddress",
            ["department"] = "ou"
        };
        var optionsWithCustomMappings = new SamlOptions
        {
            DefaultClaimMappings = new ReadOnlyDictionary<string, string>(customMappings)
        };
        var service = new SamlClaimsService(_profileService, NullLogger<SamlClaimsService>.Instance, Options.Create(optionsWithCustomMappings));

        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("sub", "test123"),
            new Claim("email", "test@example.com"),
            new Claim("department", "Engineering"),
            new Claim("unmapped", "value")
        }));

        var sp = new SamlServiceProvider
        {
            EntityId = "https://sp.example.com",
            DisplayName = "Test Service Provider",
            AssertionConsumerServiceUrls = new[] { new Uri("https://sp.example.com/acs") }
        };

        _profileService.ProfileClaims = user.Claims.ToList();

        // Act
        var attributes = (await service.GetMappedAttributesAsync(user, sp, _ct)).ToList();

        // Assert
        attributes.Count.ShouldBe(2); // Only email and department are mapped; sub and unmapped are excluded
        attributes.ShouldContain(a => a.Name == "emailAddress");
        attributes.ShouldContain(a => a.Name == "ou");
        attributes.ShouldNotContain(a => a.Name == "sub");
        attributes.ShouldNotContain(a => a.Name == "unmapped");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task service_provider_mappings_should_override_global()
    {
        // Arrange
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("sub", "test123"),
            new Claim("email", "test@example.com"),
            new Claim("department", "Engineering")
        }));

        var sp = new SamlServiceProvider
        {
            EntityId = "https://sp.example.com",
            DisplayName = "Test Service Provider",
            AssertionConsumerServiceUrls = new[] { new Uri("https://sp.example.com/acs") },
            ClaimMappings = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>
            {
                ["email"] = "mail",  // Override default OID mapping
                ["department"] = "businessUnit"
            })
        };

        _profileService.ProfileClaims = user.Claims.ToList();

        // Act
        var attributes = (await _service.GetMappedAttributesAsync(user, sp, _ct)).ToList();

        // Assert
        attributes.Count.ShouldBe(2); // email and department from SP mappings; sub not mapped
        attributes.ShouldContain(a => a.Name == "mail" && a.Values[0] == "test@example.com");
        attributes.ShouldContain(a => a.Name == "businessUnit" && a.Values[0] == "Engineering");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task service_provider_mappings_should_fall_back_to_global_for_unmapped()
    {
        // Arrange
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("sub", "test123"),
            new Claim("email", "test@example.com"),
            new Claim("given_name", "John")
        }));

        var sp = new SamlServiceProvider
        {
            EntityId = "https://sp.example.com",
            DisplayName = "Test Service Provider",
            AssertionConsumerServiceUrls = new[] { new Uri("https://sp.example.com/acs") },
            ClaimMappings = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>
            {
                ["email"] = "mail"  // Override only email
            })
        };

        _profileService.ProfileClaims = user.Claims.ToList();

        // Act
        var attributes = (await _service.GetMappedAttributesAsync(user, sp, _ct)).ToList();

        // Assert
        attributes.Count.ShouldBe(1); // Only email is mapped (overridden by SP); sub and given_name are not in defaults
        attributes.ShouldContain(a => a.Name == "mail");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task multi_valued_claims_should_group_into_single_attribute()
    {
        // Arrange
        var customMappings = new Dictionary<string, string>
        {
            ["sub"] = "urn:oid:0.9.2342.19200300.100.1.1",
            ["role"] = "role" // Map role to itself for this test
        };
        var optionsWithCustomMappings = new SamlOptions
        {
            DefaultClaimMappings = new ReadOnlyDictionary<string, string>(customMappings)
        };
        var service = new SamlClaimsService(_profileService, NullLogger<SamlClaimsService>.Instance, Options.Create(optionsWithCustomMappings));

        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("sub", "test123"),
            new Claim("role", "Admin"),
            new Claim("role", "User"),
            new Claim("role", "Developer")
        }));

        var sp = new SamlServiceProvider
        {
            EntityId = "https://sp.example.com",
            DisplayName = "Test Service Provider",
            AssertionConsumerServiceUrls = new[] { new Uri("https://sp.example.com/acs") }
        };

        _profileService.ProfileClaims = user.Claims.ToList();

        // Act
        var attributes = (await service.GetMappedAttributesAsync(user, sp, _ct)).ToList();

        // Assert
        attributes.Count.ShouldBe(2); // sub + role (multi-valued)

        var roleAttr = attributes.First(a => a.Name == "role");
        roleAttr.Values.Count.ShouldBe(3);
        roleAttr.Values.ShouldContain("Admin");
        roleAttr.Values.ShouldContain("User");
        roleAttr.Values.ShouldContain("Developer");

        attributes.ShouldContain(a => a.Name == "urn:oid:0.9.2342.19200300.100.1.1"); // sub mapped to OID
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task custom_mapper_should_use_custom_mapper()
    {
        // Arrange
        var customMapper = new TestSamlClaimsMapper();
        var service = new SamlClaimsService(_profileService, NullLogger<SamlClaimsService>.Instance, _options, customMapper);

        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("sub", "test123"),
            new Claim("email", "test@example.com")
        }));

        var sp = new SamlServiceProvider
        {
            EntityId = "https://sp.example.com",
            DisplayName = "Test Service Provider",
            AssertionConsumerServiceUrls = new[] { new Uri("https://sp.example.com/acs") }
        };

        _profileService.ProfileClaims = user.Claims.ToList();

        // Act
        var attributes = (await service.GetMappedAttributesAsync(user, sp, _ct)).ToList();

        // Assert
        attributes.Count.ShouldBe(1);
        attributes.First().Name.ShouldBe("CUSTOM_MAPPED");
        attributes.First().Values.Count.ShouldBe(1);
        attributes.First().Values[0].ShouldBe("custom_value");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task should_set_correct_attribute_name_format()
    {
        // Arrange
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("sub", "test123"),
            new Claim("email", "test@example.com")
        }));

        var sp = new SamlServiceProvider
        {
            EntityId = "https://sp.example.com",
            DisplayName = "Test Service Provider",
            AssertionConsumerServiceUrls = new[] { new Uri("https://sp.example.com/acs") }
        };

        _profileService.ProfileClaims = user.Claims.ToList();

        // Act
        var attributes = (await _service.GetMappedAttributesAsync(user, sp, _ct)).ToList();

        // Assert
        attributes.ShouldAllBe(a => a.NameFormat == _samlOptions.DefaultAttributeNameFormat);
    }
}
