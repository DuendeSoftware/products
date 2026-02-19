// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Collections.ObjectModel;
using System.Net;
using System.Security.Claims;
using Duende.IdentityModel;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Saml;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;
using static Duende.IdentityServer.IntegrationTests.Endpoints.Saml.SamlTestHelpers;

namespace Duende.IdentityServer.IntegrationTests.Endpoints.Saml;

public class SamlClaimsMappingTests(ITestOutputHelper output)
{
    private const string Category = "SAML Claims Mapping";

    private SamlFixture Fixture = new(output);
    private SamlDataBuilder Build => Fixture.Builder;

    [Fact]
    [Trait("Category", Category)]
    public async Task claims_should_use_default_mappings_for_standard_claims()
    {
        // Arrange - default mappings should be active
        Fixture.ServiceProviders.Add(Build.SamlServiceProvider());
        await Fixture.InitializeAsync();

        var claims = new List<Claim>
        {
            new(JwtClaimTypes.Subject, "user123"),
            new("name", "John Doe"),
            new("email", "john@example.com"),
            new("role", "Admin")
        };

        Fixture.UserToSignIn = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
        await Fixture.Client.GetAsync("/__signin", CancellationToken.None);

        var authnRequestXml = Build.AuthNRequestXml();
        var urlEncoded = await EncodeRequest(authnRequestXml, CancellationToken.None);

        // Act
        var result = await Fixture.Client.GetAsync($"/saml/signin?SAMLRequest={urlEncoded}", CancellationToken.None);

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.OK);
        var successResponse = await ExtractSamlSuccessFromPostAsync(result, CancellationToken.None);

        // Verify mapped attributes are present with correct names
        var attributes = successResponse.Assertion.Attributes;
        attributes.ShouldNotBeNull();

        var nameAttr = attributes.FirstOrDefault(a => a.Name == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name");
        nameAttr.ShouldNotBeNull();
        nameAttr.Value.ShouldBe("John Doe");

        var emailAttr = attributes.FirstOrDefault(a => a.Name == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress");
        emailAttr.ShouldNotBeNull();
        emailAttr.Value.ShouldBe("john@example.com");

        var roleAttr = attributes.FirstOrDefault(a => a.Name == "http://schemas.xmlsoap.org/ws/2005/05/identity/role");
        roleAttr.ShouldNotBeNull();
        roleAttr.Value.ShouldBe("Admin");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task unmapped_claims_should_be_excluded_from_assertion()
    {
        // Arrange - only default mappings active
        Fixture.ServiceProviders.Add(Build.SamlServiceProvider());
        await Fixture.InitializeAsync();

        var claims = new List<Claim>
        {
            new(JwtClaimTypes.Subject, "user123"),
            new("name", "John Doe"),
            new("custom_claim_not_mapped", "should not appear"),
            new("another_unmapped", "also excluded")
        };

        Fixture.UserToSignIn = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
        await Fixture.Client.GetAsync("/__signin", CancellationToken.None);

        var authnRequestXml = Build.AuthNRequestXml();
        var urlEncoded = await EncodeRequest(authnRequestXml, CancellationToken.None);

        // Act
        var result = await Fixture.Client.GetAsync($"/saml/signin?SAMLRequest={urlEncoded}", CancellationToken.None);

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.OK);
        var successResponse = await ExtractSamlSuccessFromPostAsync(result, CancellationToken.None);

        var attributes = successResponse.Assertion.Attributes;
        attributes.ShouldNotBeNull();

        // Verify only mapped claim (name) is present
        var nameAttr = attributes.FirstOrDefault(a => a.Name == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name");
        nameAttr.ShouldNotBeNull();
        nameAttr.Value.ShouldBe("John Doe");

        // Verify unmapped claims are excluded
        attributes.ShouldNotContain(a => a.Name != null && a.Name.Contains("custom_claim"));
        attributes.ShouldNotContain(a => a.Name != null && a.Name.Contains("another_unmapped"));
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task service_provider_mappings_should_override_global_defaults()
    {
        // Arrange - SP with custom claim mappings
        var spWithCustomMappings = Build.SamlServiceProvider();
        spWithCustomMappings.ClaimMappings = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>
        {
            ["email"] = "mail", // Override default mapping
            ["department"] = "ou" // Custom mapping
        });

        Fixture.ServiceProviders.Add(spWithCustomMappings);
        await Fixture.InitializeAsync();

        var claims = new List<Claim>
        {
            new(JwtClaimTypes.Subject, "user123"),
            new("email", "jane@example.com"),
            new("department", "Engineering")
        };

        Fixture.UserToSignIn = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
        await Fixture.Client.GetAsync("/__signin", CancellationToken.None);

        var authnRequestXml = Build.AuthNRequestXml();
        var urlEncoded = await EncodeRequest(authnRequestXml, CancellationToken.None);

        // Act
        var result = await Fixture.Client.GetAsync($"/saml/signin?SAMLRequest={urlEncoded}", CancellationToken.None);

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.OK);
        var successResponse = await ExtractSamlSuccessFromPostAsync(result, CancellationToken.None);

        var attributes = successResponse.Assertion.Attributes;
        attributes.ShouldNotBeNull();

        // Verify email uses SP's custom mapping (not default)
        var emailAttr = attributes.FirstOrDefault(a => a.Name == "mail");
        emailAttr.ShouldNotBeNull();
        emailAttr.Value.ShouldBe("jane@example.com");

        // Verify default email mapping is NOT present
        attributes.ShouldNotContain(a => a.Name == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress");

        // Verify custom department mapping
        var deptAttr = attributes.FirstOrDefault(a => a.Name == "ou");
        deptAttr.ShouldNotBeNull();
        deptAttr.Value.ShouldBe("Engineering");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task custom_claim_mapper_should_replace_default_mapping_logic()
    {
        // Arrange - register custom mapper via ConfigureServices
        var customMapper = new TestSamlClaimsMapper();
        Fixture.ConfigureServices = services =>
        {
            services.AddSingleton<ISamlClaimsMapper>(customMapper);
        };

        Fixture.ServiceProviders.Add(Build.SamlServiceProvider());
        await Fixture.InitializeAsync();

        var claims = new List<Claim>
        {
            new(JwtClaimTypes.Subject, "user123"),
            new("email", "test@example.com"),
            new("name", "Should be ignored")
        };

        Fixture.UserToSignIn = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
        await Fixture.Client.GetAsync("/__signin", CancellationToken.None);

        var authnRequestXml = Build.AuthNRequestXml();
        var urlEncoded = await EncodeRequest(authnRequestXml, CancellationToken.None);

        // Act
        var result = await Fixture.Client.GetAsync($"/saml/signin?SAMLRequest={urlEncoded}", CancellationToken.None);

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.OK);
        var successResponse = await ExtractSamlSuccessFromPostAsync(result, CancellationToken.None);

        var attributes = successResponse.Assertion.Attributes;
        attributes.ShouldNotBeNull();

        // Verify custom mapper output appears
        var customAttr = attributes.FirstOrDefault(a => a.Name == "CUSTOM_MAPPED");
        customAttr.ShouldNotBeNull();
        customAttr.Value.ShouldBe("custom_value");

        // Verify default mappings were NOT applied (custom mapper replaces everything)
        attributes.ShouldNotContain(a => a.Name == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name");
        attributes.ShouldNotContain(a => a.Name == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task multi_valued_claims_should_be_grouped_into_single_attribute()
    {
        // Arrange
        Fixture.ServiceProviders.Add(Build.SamlServiceProvider());
        await Fixture.InitializeAsync();

        var claims = new List<Claim>
        {
            new(JwtClaimTypes.Subject, "user123"),
            new("role", "Admin"),
            new("role", "User"),
            new("role", "Manager")
        };

        Fixture.UserToSignIn = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
        await Fixture.Client.GetAsync("/__signin", CancellationToken.None);

        var authnRequestXml = Build.AuthNRequestXml();
        var urlEncoded = await EncodeRequest(authnRequestXml, CancellationToken.None);

        // Act
        var result = await Fixture.Client.GetAsync($"/saml/signin?SAMLRequest={urlEncoded}", CancellationToken.None);

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.OK);
        var successResponse = await ExtractSamlSuccessFromPostAsync(result, CancellationToken.None);

        var attributes = successResponse.Assertion.Attributes;
        attributes.ShouldNotBeNull();

        // Verify only one role attribute exists
        var roleAttributes = attributes.Where(a => a.Name == "http://schemas.xmlsoap.org/ws/2005/05/identity/role").ToList();
        roleAttributes.Count.ShouldBe(1);

        // Verify it has all three values
        var roleAttr = roleAttributes.First();
        roleAttr.Values.Count.ShouldBe(3);
        roleAttr.Values.ShouldContain("Admin");
        roleAttr.Values.ShouldContain("User");
        roleAttr.Values.ShouldContain("Manager");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task custom_global_mappings_should_apply_to_all_service_providers()
    {
        // Arrange - configure custom global mappings via ConfigureServices
        Fixture.ConfigureServices = services =>
        {
            // Replace the registered SamlOptions with our custom instance
            services.AddSingleton(Microsoft.Extensions.Options.Options.Create(new SamlOptions
            {
                DefaultClaimMappings = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>
                {
                    ["email"] = "emailAddress",
                    ["department"] = "dept"
                })
            }));
        };

        Fixture.ServiceProviders.Add(Build.SamlServiceProvider());
        await Fixture.InitializeAsync();

        var claims = new List<Claim>
        {
            new(JwtClaimTypes.Subject, "user123"),
            new("email", "test@example.com"),
            new("department", "Sales")
        };

        Fixture.UserToSignIn = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
        await Fixture.Client.GetAsync("/__signin", CancellationToken.None);

        var authnRequestXml = Build.AuthNRequestXml();
        var urlEncoded = await EncodeRequest(authnRequestXml, CancellationToken.None);

        // Act
        var result = await Fixture.Client.GetAsync($"/saml/signin?SAMLRequest={urlEncoded}", CancellationToken.None);

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.OK);
        var successResponse = await ExtractSamlSuccessFromPostAsync(result, CancellationToken.None);

        var attributes = successResponse.Assertion.Attributes;
        attributes.ShouldNotBeNull();

        // Verify custom mappings are used
        var emailAttr = attributes.FirstOrDefault(a => a.Name == "emailAddress");
        emailAttr.ShouldNotBeNull();
        emailAttr.Value.ShouldBe("test@example.com");

        var deptAttr = attributes.FirstOrDefault(a => a.Name == "dept");
        deptAttr.ShouldNotBeNull();
        deptAttr.Value.ShouldBe("Sales");
    }
}
