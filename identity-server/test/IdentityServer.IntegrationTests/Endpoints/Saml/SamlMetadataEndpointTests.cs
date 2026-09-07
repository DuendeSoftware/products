// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Saml;
using Duende.IdentityServer.Stores;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Duende.IdentityServer.IntegrationTests.Endpoints.Saml;

public class SamlMetadataEndpointTests
{
    private const string Category = "SAML Metadata Endpoint";

    private static readonly XNamespace Md = XNamespace.Get("urn:oasis:names:tc:SAML:2.0:metadata");
    private static readonly XNamespace Ds = XNamespace.Get("http://www.w3.org/2000/09/xmldsig#");

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

    [Fact]
    [Trait("Category", Category)]
    public async Task metadata_should_include_multiple_key_descriptors_when_multiple_signing_credentials_are_registered()
    {
        var secondCert = SamlTestHelpers.CreateTestSigningCertificate(TimeProvider.System, "CN=second-signing-key");

        Fixture.ConfigureServices = services =>
        {
            var key = new X509SecurityKey(secondCert);
            var credential = new SigningCredentials(key, SecurityAlgorithms.RsaSha256);
            services.AddSingleton<ISigningCredentialStore>(new InMemorySigningCredentialsStore(credential));
        };

        await Fixture.InitializeAsync();

        var result = await Fixture.Client.GetAsync("/saml2", _ct);
        result.StatusCode.ShouldBe(HttpStatusCode.OK);

        var content = await result.Content.ReadAsStringAsync(_ct);
        var doc = XDocument.Parse(content);

        var keyDescriptors = doc.Descendants(Md + "KeyDescriptor").ToList();
        keyDescriptors.Count.ShouldBe(2, "Expected one KeyDescriptor per registered ISigningCredentialStore (1 primary + 1 additional)");

        foreach (var kd in keyDescriptors)
        {
            kd.Attribute("use").ShouldNotBeNull().Value.ShouldBe("signing");
        }

        var certValues = keyDescriptors
            .Select(kd => kd.Descendants(Ds + "X509Certificate").Single().Value)
            .ToList();
        certValues[0].ShouldNotBe(certValues[1]);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task metadata_should_include_key_descriptors_for_all_signing_credential_stores()
    {
        // Simulate three signing keys (e.g., active + announced + previous)
        var secondCert = SamlTestHelpers.CreateTestSigningCertificate(TimeProvider.System, "CN=announced-key");
        var thirdCert = SamlTestHelpers.CreateTestSigningCertificate(TimeProvider.System, "CN=previous-key");

        Fixture.ConfigureServices = services =>
        {
            foreach (var cert in new[] { secondCert, thirdCert })
            {
                var key = new X509SecurityKey(cert);
                var credential = new SigningCredentials(key, SecurityAlgorithms.RsaSha256);
                services.AddSingleton<ISigningCredentialStore>(new InMemorySigningCredentialsStore(credential));
            }
        };

        await Fixture.InitializeAsync();

        var result = await Fixture.Client.GetAsync("/saml2", _ct);
        result.StatusCode.ShouldBe(HttpStatusCode.OK);

        var content = await result.Content.ReadAsStringAsync(_ct);
        var doc = XDocument.Parse(content);

        var keyDescriptors = doc.Descendants(Md + "KeyDescriptor").ToList();
        keyDescriptors.Count.ShouldBe(3, "Expected one KeyDescriptor per registered ISigningCredentialStore (1 primary + 2 additional)");

        var certValues = keyDescriptors
            .Select(kd => kd.Descendants(Ds + "X509Certificate").Single().Value)
            .ToList();
        certValues.Distinct().Count().ShouldBe(3);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task metadata_should_not_include_validation_only_keys()
    {
        // Validation keys (e.g., retired keys kept for token validation) should NOT appear
        // in SAML metadata since it only publishes signing credentials
        var validationOnlyCert = SamlTestHelpers.CreateTestSigningCertificate(TimeProvider.System, "CN=validation-only");

        Fixture.ConfigureServices = services =>
        {
            var keyInfo = new SecurityKeyInfo
            {
                Key = new X509SecurityKey(validationOnlyCert),
                SigningAlgorithm = SecurityAlgorithms.RsaSha256
            };
            services.AddSingleton<IValidationKeysStore>(new InMemoryValidationKeysStore([keyInfo]));
        };

        await Fixture.InitializeAsync();

        var result = await Fixture.Client.GetAsync("/saml2", _ct);
        result.StatusCode.ShouldBe(HttpStatusCode.OK);

        var content = await result.Content.ReadAsStringAsync(_ct);
        var doc = XDocument.Parse(content);

        var keyDescriptors = doc.Descendants(Md + "KeyDescriptor").ToList();
        keyDescriptors.Count.ShouldBe(1, "Only signing credentials should appear; validation-only keys should be excluded");

        // Verify the published key is the primary signing cert, not the validation-only cert
        var publishedCertValue = keyDescriptors.Single().Descendants(Ds + "X509Certificate").Single().Value;
        var validationOnlyCertBase64 = Convert.ToBase64String(validationOnlyCert.RawData);
        publishedCertValue.ShouldNotBe(validationOnlyCertBase64);
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
