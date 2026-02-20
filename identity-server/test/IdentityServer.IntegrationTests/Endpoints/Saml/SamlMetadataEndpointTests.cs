// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using System.Xml.Linq;
using Duende.IdentityServer.Internal.Saml;

namespace Duende.IdentityServer.IntegrationTests.Endpoints.Saml;

public class SamlMetadataEndpointTests
{
    private const string Category = "SAML Metadata Endpoint";

    private SamlFixture Fixture = new();

    [Fact]
    [Trait("Category", Category)]
    public async Task metadata_endpoint_should_return_metadata()
    {
        await Fixture.InitializeAsync();

        var result = await Fixture.Client.GetAsync("/saml/metadata", CancellationToken.None);
        result.StatusCode.ShouldBe(HttpStatusCode.OK);
        result.Content.Headers.ContentType
            .ShouldNotBeNull()
            .MediaType
            .ShouldBe(SamlConstants.ContentTypes.Metadata);

        var content = await result.Content.ReadAsStringAsync(CancellationToken.None);

        var settings = new VerifySettings();
        var hostUri = Fixture.Url();
        settings.AddScrubber(sb =>
        {
            sb.Replace(hostUri, "https://localhost");
        });

        await Verify(content, settings);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task metadata_should_include_valid_until_based_on_metadata_validity_duration()
    {
        Fixture.ConfigureSamlOptions = options =>
        {
            options.MetadataValidityDuration = TimeSpan.FromDays(30);
        };

        await Fixture.InitializeAsync();

        var result = await Fixture.Client.GetAsync("/saml/metadata", CancellationToken.None);
        result.StatusCode.ShouldBe(HttpStatusCode.OK);

        var content = await result.Content.ReadAsStringAsync(CancellationToken.None);

        var expectedValidUntil = Fixture.Now.Add(TimeSpan.FromDays(30)).UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ");
        content.ShouldContain($"validUntil=\"{expectedValidUntil}\"");
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

        var result = await Fixture.Client.GetAsync("/saml/metadata", CancellationToken.None);
        result.StatusCode.ShouldBe(HttpStatusCode.OK);

        var content = await result.Content.ReadAsStringAsync(CancellationToken.None);

        content.ShouldContain("WantAuthnRequestsSigned=\"true\"");
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

        var result = await Fixture.Client.GetAsync("/saml/metadata", CancellationToken.None);
        result.StatusCode.ShouldBe(HttpStatusCode.OK);

        var content = await result.Content.ReadAsStringAsync(CancellationToken.None);

        content.ShouldContain("<NameIDFormat>urn:oasis:names:tc:SAML:1.1:nameid-format:emailAddress</NameIDFormat>");
        content.ShouldContain("<NameIDFormat>urn:oasis:names:tc:SAML:2.0:nameid-format:persistent</NameIDFormat>");
        content.ShouldNotContain("<NameIDFormat>urn:oasis:names:tc:SAML:2.0:nameid-format:transient</NameIDFormat>");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task metadata_should_include_single_logout_service_endpoints()
    {
        await Fixture.InitializeAsync();

        var result = await Fixture.Client.GetAsync("/saml/metadata", CancellationToken.None);
        result.StatusCode.ShouldBe(HttpStatusCode.OK);

        var content = await result.Content.ReadAsStringAsync(CancellationToken.None);

        content.ShouldContain("<SingleLogoutService");
        content.ShouldContain("Binding=\"urn:oasis:names:tc:SAML:2.0:bindings:HTTP-POST\"");
        content.ShouldContain("Binding=\"urn:oasis:names:tc:SAML:2.0:bindings:HTTP-Redirect\"");
        content.ShouldContain("/saml/logout");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task metadata_urls_should_not_have_trailing_slashes()
    {
        await Fixture.InitializeAsync();

        var result = await Fixture.Client.GetAsync("/saml/metadata", CancellationToken.None);
        result.StatusCode.ShouldBe(HttpStatusCode.OK);

        var content = await result.Content.ReadAsStringAsync(CancellationToken.None);
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

        var result = await Fixture.Client.GetAsync("/saml/metadata", CancellationToken.None);
        result.StatusCode.ShouldBe(HttpStatusCode.OK);

        var content = await result.Content.ReadAsStringAsync(CancellationToken.None);
        var locationUrls = GetServiceLocationUrls(content, "SingleSignOnService", "SingleLogoutService");

        foreach (var location in locationUrls)
        {
            var uri = new Uri(location);
            uri.PathAndQuery.ShouldNotContain("//");
        }
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task metadata_urls_should_be_correct_when_route_has_trailing_slash()
    {
        Fixture.ConfigureSamlOptions = options =>
        {
            // Configure with trailing slash to ensure it's handled correctly
            options.UserInteraction.Route = "/saml/";
        };

        await Fixture.InitializeAsync();

        var result = await Fixture.Client.GetAsync("/saml/metadata", CancellationToken.None);
        result.StatusCode.ShouldBe(HttpStatusCode.OK);

        var content = await result.Content.ReadAsStringAsync(CancellationToken.None);

        // Should not have double slashes
        content.ShouldNotContain("saml//signin");
        content.ShouldNotContain("saml//logout");

        var locationUrls = GetServiceLocationUrls(content, "SingleSignOnService", "SingleLogoutService");

        foreach (var location in locationUrls)
        {
            location.ShouldNotEndWith("/");
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

        var result = await Fixture.Client.GetAsync("/saml/metadata", CancellationToken.None);
        result.StatusCode.ShouldBe(HttpStatusCode.OK);

        var content = await result.Content.ReadAsStringAsync(CancellationToken.None);
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
