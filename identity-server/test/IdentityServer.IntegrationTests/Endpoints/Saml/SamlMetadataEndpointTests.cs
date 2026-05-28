// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Duende.IdentityServer.Saml;

namespace Duende.IdentityServer.IntegrationTests.Endpoints.Saml;

public class SamlMetadataEndpointTests
{
    private const string Category = "SAML Metadata Endpoint";

    private readonly Ct _ct = TestContext.Current.CancellationToken;

    private SamlFixture Fixture = new();

    [Fact]
    [Trait("Category", Category)]
    public async Task metadata_endpoint_should_return_metadata()
    {
        await Fixture.InitializeAsync();

        var result = await Fixture.Client.GetAsync("/saml2", _ct);
        result.StatusCode.ShouldBe(HttpStatusCode.OK);
        result.Content.Headers.ContentType
            .ShouldNotBeNull()
            .MediaType
            .ShouldBe(SamlConstants.ContentTypes.Metadata);

        var content = await result.Content.ReadAsStringAsync(_ct);

        var settings = new VerifySettings();
        var hostUri = Fixture.Url();
        settings.AddScrubber(sb =>
        {
            sb.Replace(hostUri, "https://localhost");
        });
        settings.AddScrubber(sb =>
        {
            var scrubbed = Regex.Replace(sb.ToString(), @"ID=""[^""]+""", @"ID=""_SCRUBBED""");
            sb.Clear().Append(scrubbed);
        });

        await Verify(content, settings);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task metadata_should_include_valid_until_based_on_metadata_validity_duration()
    {
        Fixture.ConfigureSamlOptions = options =>
        {
            options.Metadata.ExpiryDuration = TimeSpan.FromDays(30);
        };

        await Fixture.InitializeAsync();

        var result = await Fixture.Client.GetAsync("/saml2", _ct);
        result.StatusCode.ShouldBe(HttpStatusCode.OK);

        var content = await result.Content.ReadAsStringAsync(_ct);
        var doc = XDocument.Parse(content);

        var expectedValidUntil = Fixture.Now.Add(TimeSpan.FromDays(30)).UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ");
        doc.Root.ShouldNotBeNull()
            .Attribute("validUntil")
            .ShouldNotBeNull()
            .Value
            .ShouldBe(expectedValidUntil);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task metadata_should_include_want_authn_requests_signed_when_enabled()
    {
        Fixture.ConfigureSamlOptions = options =>
        {
            options.WantAuthnRequestsSigned = true;
        };

        await Fixture.InitializeAsync();

        var result = await Fixture.Client.GetAsync("/saml2", _ct);
        result.StatusCode.ShouldBe(HttpStatusCode.OK);

        var content = await result.Content.ReadAsStringAsync(_ct);
        var doc = XDocument.Parse(content);
        var md = XNamespace.Get("urn:oasis:names:tc:SAML:2.0:metadata");

        var idpDescriptor = doc.Descendants(md + "IDPSSODescriptor").Single();
        idpDescriptor.Attribute("WantAuthnRequestsSigned")
            .ShouldNotBeNull()
            .Value
            .ShouldBe("true");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task metadata_should_include_supported_name_id_formats()
    {
        Fixture.ConfigureSamlOptions = options =>
        {
            options.SupportedNameIdFormats.Clear();
            options.SupportedNameIdFormats.Add(SamlConstants.NameIdentifierFormats.EmailAddress);
            options.SupportedNameIdFormats.Add(SamlConstants.NameIdentifierFormats.Persistent);
        };

        await Fixture.InitializeAsync();

        var result = await Fixture.Client.GetAsync("/saml2", _ct);
        result.StatusCode.ShouldBe(HttpStatusCode.OK);

        var content = await result.Content.ReadAsStringAsync(_ct);
        var doc = XDocument.Parse(content);
        var md = XNamespace.Get("urn:oasis:names:tc:SAML:2.0:metadata");

        var formats = doc.Descendants(md + "NameIDFormat")
            .Select(e => e.Value)
            .ToList();

        formats.Count.ShouldBe(2);
        formats[0].ShouldBe(SamlConstants.NameIdentifierFormats.EmailAddress);
        formats[1].ShouldBe(SamlConstants.NameIdentifierFormats.Persistent);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task metadata_should_include_single_logout_service_endpoints()
    {
        await Fixture.InitializeAsync();

        var result = await Fixture.Client.GetAsync("/saml2", _ct);
        result.StatusCode.ShouldBe(HttpStatusCode.OK);

        var content = await result.Content.ReadAsStringAsync(_ct);
        var doc = XDocument.Parse(content);
        var ns = XNamespace.Get("urn:oasis:names:tc:SAML:2.0:metadata");

        var sloServices = doc.Descendants(ns + "SingleLogoutService").ToList();
        sloServices.ShouldNotBeEmpty("Metadata should include SingleLogoutService elements");

        var bindings = sloServices.Select(s => s.Attribute("Binding")?.Value).ToList();
        bindings.ShouldContain("urn:oasis:names:tc:SAML:2.0:bindings:HTTP-POST");
        bindings.ShouldContain("urn:oasis:names:tc:SAML:2.0:bindings:HTTP-Redirect");

        foreach (var slo in sloServices)
        {
            slo.Attribute("Location")!.Value.ShouldContain("/Saml2/SLO");
        }
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task metadata_urls_should_not_have_trailing_slashes()
    {
        await Fixture.InitializeAsync();

        var result = await Fixture.Client.GetAsync("/saml2", _ct);
        result.StatusCode.ShouldBe(HttpStatusCode.OK);

        var content = await result.Content.ReadAsStringAsync(_ct);
        var locationUrls = GetServiceLocationUrls(content, "SingleSignOnService", "SingleLogoutService");

        foreach (var location in locationUrls)
        {
            location.ShouldNotEndWith("/", $"Service Location should not end with trailing slash: {location}");
        }
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task metadata_urls_should_not_contain_double_slashes()
    {
        await Fixture.InitializeAsync();

        var result = await Fixture.Client.GetAsync("/saml2", _ct);
        result.StatusCode.ShouldBe(HttpStatusCode.OK);

        var content = await result.Content.ReadAsStringAsync(_ct);
        var locationUrls = GetServiceLocationUrls(content, "SingleSignOnService", "SingleLogoutService");

        foreach (var location in locationUrls)
        {
            var uri = new Uri(location);
            uri.PathAndQuery.ShouldNotContain("//");
        }
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task metadata_urls_should_handle_edge_case_route_configurations()
    {
        // Verify that default configuration produces clean URLs
        // Edge cases (empty routes, whitespace, etc.) are comprehensively tested
        // at the unit level in BuildServiceUrl unit tests
        await Fixture.InitializeAsync();

        var result = await Fixture.Client.GetAsync("/saml2", _ct);
        result.StatusCode.ShouldBe(HttpStatusCode.OK);

        var content = await result.Content.ReadAsStringAsync(_ct);
        var locationUrls = GetServiceLocationUrls(content, "SingleSignOnService", "SingleLogoutService");

        foreach (var location in locationUrls)
        {
            // Should have clean paths without double slashes
            var uri = new Uri(location);
            uri.PathAndQuery.ShouldNotContain("//");

            // Should not have trailing slashes
            location.ShouldNotEndWith("/");
        }
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task metadata_should_be_served_at_custom_entity_id_path()
    {
        Fixture.ConfigureSamlOptions = options =>
        {
            options.EntityId = "https://idp.example.com/custom/saml";
        };

        await Fixture.InitializeAsync();

        // Metadata should be available at the path component of the entity ID
        var result = await Fixture.Client.GetAsync("/custom/saml", _ct);
        result.StatusCode.ShouldBe(HttpStatusCode.OK);
        result.Content.Headers.ContentType
            .ShouldNotBeNull()
            .MediaType
            .ShouldBe(SamlConstants.ContentTypes.Metadata);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task metadata_should_not_be_served_at_old_path_when_entity_id_is_custom()
    {
        Fixture.ConfigureSamlOptions = options =>
        {
            options.EntityId = "https://idp.example.com/custom/saml";
        };

        await Fixture.InitializeAsync();

        // Default path should no longer serve metadata
        var result = await Fixture.Client.GetAsync("/saml2", _ct);
        result.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task metadata_should_fall_back_to_metadata_path_for_urn_entity_id()
    {
        Fixture.ConfigureSamlOptions = options =>
        {
            options.EntityId = "urn:my:custom:idp";
        };

        await Fixture.InitializeAsync();

        // URN entity IDs can't derive a path, so fall back to Endpoints.MetadataPath (default: /Saml2)
        var result = await Fixture.Client.GetAsync("/saml2", _ct);
        result.StatusCode.ShouldBe(HttpStatusCode.OK);
        result.Content.Headers.ContentType
            .ShouldNotBeNull()
            .MediaType
            .ShouldBe(SamlConstants.ContentTypes.Metadata);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task metadata_should_include_single_logout_service_elements()
    {
        await Fixture.InitializeAsync();

        var result = await Fixture.Client.GetAsync("/saml2", _ct);
        result.StatusCode.ShouldBe(HttpStatusCode.OK);

        var content = await result.Content.ReadAsStringAsync(_ct);
        var doc = XDocument.Parse(content);
        var ns = XNamespace.Get("urn:oasis:names:tc:SAML:2.0:metadata");

        var sloServices = doc.Descendants(ns + "SingleLogoutService").ToList();
        sloServices.ShouldNotBeEmpty("Metadata should include SingleLogoutService elements");

        foreach (var slo in sloServices)
        {
            slo.Attribute("Binding").ShouldNotBeNull();
            slo.Attribute("Location").ShouldNotBeNull();
            slo.Attribute("Location")!.Value.ShouldContain("/Saml2/SLO");
        }
    }

    private static List<string> GetServiceLocationUrls(string xmlContent, params string[] serviceElementNames)
    {
        var doc = XDocument.Parse(xmlContent);
        var ns = XNamespace.Get("urn:oasis:names:tc:SAML:2.0:metadata");
        var locations = new List<string>();

        foreach (var serviceName in serviceElementNames)
        {
            var services = doc.Descendants(ns + serviceName);
            foreach (var service in services)
            {
                var location = service.Attribute("Location")?.Value;
                location.ShouldNotBeNull($"{serviceName} should have a Location attribute");
                locations.Add(location);
            }
        }

        return locations;
    }
}
