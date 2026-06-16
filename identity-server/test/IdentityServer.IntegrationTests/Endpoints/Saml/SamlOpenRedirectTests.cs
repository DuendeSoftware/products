// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System.Net;
using System.Text.RegularExpressions;
using Duende.IdentityServer.Saml.Models;
using static Duende.IdentityServer.IntegrationTests.Endpoints.Saml.SamlTestHelpers;

namespace Duende.IdentityServer.IntegrationTests.Endpoints.Saml;

/// <summary>
/// Regression tests verifying that validation errors never produce SAML responses
/// directed at an attacker-controlled ACS URL. These tests exercise the attack
/// vector where a malicious AuthnRequest supplies an unregistered ACS URL and
/// triggers a protocol error (e.g., invalid version) that fires before ACS
/// validation. The expected safe behavior is that all such errors render the
/// local error page rather than POSTing a SAML error response to the
/// unvalidated URL.
/// </summary>
public sealed class SamlOpenRedirectTests
{
    private const string Category = "SAML Open Redirect";
    private const string AttackerAcsUrl = "https://evil.example.com/steal";

    private readonly Ct _ct = TestContext.Current.CancellationToken;

    private SamlFixture Fixture = new();

    private SamlData Data => Fixture.Data;

    private SamlDataBuilder Build => Fixture.Builder;

    [Fact]
    [Trait("Category", Category)]
    public async Task invalid_version_with_malicious_acs_does_not_redirect_to_attacker()
    {
        Fixture.ServiceProviders.Add(Build.SamlServiceProvider());
        await Fixture.InitializeAsync();

        // Version "1.0" triggers validation failure before ACS URL validation
        var authnRequestXml = Build.AuthNRequestXml(
            version: "1.0",
            acsUrl: new Uri(AttackerAcsUrl));

        var urlEncoded = await EncodeRequest(authnRequestXml);
        var result = await Fixture.Client.GetAsync($"/Saml2/SSO?SAMLRequest={urlEncoded}", _ct);

        await AssertErrorPageNotAttackerUrl(result);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task invalid_destination_with_malicious_acs_does_not_redirect_to_attacker()
    {
        Fixture.ServiceProviders.Add(Build.SamlServiceProvider());
        await Fixture.InitializeAsync();

        // Wrong destination triggers validation failure before ACS URL validation
        var authnRequestXml = Build.AuthNRequestXml(
            destination: new Uri("https://wrong.example.com/saml"),
            acsUrl: new Uri(AttackerAcsUrl));

        var urlEncoded = await EncodeRequest(authnRequestXml);
        var result = await Fixture.Client.GetAsync($"/Saml2/SSO?SAMLRequest={urlEncoded}", _ct);

        await AssertErrorPageNotAttackerUrl(result);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task expired_issue_instant_with_malicious_acs_does_not_redirect_to_attacker()
    {
        Fixture.ServiceProviders.Add(Build.SamlServiceProvider());
        await Fixture.InitializeAsync();

        // Issue instant far in the past triggers validation failure before ACS URL validation
        var authnRequestXml = Build.AuthNRequestXml(
            issueInstant: Data.Now.AddHours(-2),
            acsUrl: new Uri(AttackerAcsUrl));

        var urlEncoded = await EncodeRequest(authnRequestXml);
        var result = await Fixture.Client.GetAsync($"/Saml2/SSO?SAMLRequest={urlEncoded}", _ct);

        await AssertErrorPageNotAttackerUrl(result);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task unregistered_acs_url_does_not_redirect_to_attacker()
    {
        Fixture.ServiceProviders.Add(Build.SamlServiceProvider());
        await Fixture.InitializeAsync();

        // ACS URL not registered for the SP — should fail ACS validation
        var authnRequestXml = Build.AuthNRequestXml(
            acsUrl: new Uri(AttackerAcsUrl));

        var urlEncoded = await EncodeRequest(authnRequestXml);
        var result = await Fixture.Client.GetAsync($"/Saml2/SSO?SAMLRequest={urlEncoded}", _ct);

        await AssertErrorPageNotAttackerUrl(result);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task unknown_issuer_with_malicious_acs_does_not_redirect_to_attacker()
    {
        Fixture.ServiceProviders.Add(Build.SamlServiceProvider());
        await Fixture.InitializeAsync();

        // Unknown issuer triggers SP lookup failure (very first validation step)
        var authnRequestXml = Build.AuthNRequestXml(
            issuer: "https://unknown-sp.example.com",
            acsUrl: new Uri(AttackerAcsUrl));

        var urlEncoded = await EncodeRequest(authnRequestXml);
        var result = await Fixture.Client.GetAsync($"/Saml2/SSO?SAMLRequest={urlEncoded}", _ct);

        await AssertErrorPageNotAttackerUrl(result);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task signature_required_but_missing_with_malicious_acs_does_not_redirect_to_attacker()
    {
        var signingCert = CreateTestSigningCertificate(Data.FakeTimeProvider);
        Fixture.ServiceProviders.Add(Build.SamlServiceProvider(signingCert, requireSignedAuthnRequests: true));
        await Fixture.InitializeAsync();

        // Unsigned request when signature required triggers failure before ACS validation
        var authnRequestXml = Build.AuthNRequestXml(
            acsUrl: new Uri(AttackerAcsUrl));

        var urlEncoded = await EncodeRequest(authnRequestXml);
        var result = await Fixture.Client.GetAsync($"/Saml2/SSO?SAMLRequest={urlEncoded}", _ct);

        await AssertErrorPageNotAttackerUrl(result);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task is_passive_with_registered_acs_sends_error_to_registered_acs_only()
    {
        Fixture.ServiceProviders.Add(Build.SamlServiceProvider());
        await Fixture.InitializeAsync();

        // IsPassive with a VALID registered ACS URL — this is the legitimate path
        // where SAML error responses ARE sent. Verifies the response goes to the
        // registered ACS, not the one from the request if they differed.
        var authnRequestXml = Build.AuthNRequestXml(isPassive: true);

        var urlEncoded = await EncodeRequest(authnRequestXml);
        var result = await Fixture.Client.GetAsync($"/Saml2/SSO?SAMLRequest={urlEncoded}", _ct);

        var samlError = await ExtractSamlErrorFromPostAsync(result);
        samlError.StatusCode.ShouldBe(SamlStatusCodes.Responder);
        samlError.SubStatusCode.ShouldBe(SamlStatusCodes.NoPassive);
        // The response destination must be the registered ACS URL, never an attacker URL
        samlError.AssertionConsumerServiceUrl.ShouldBe(Data.AcsUrl.OriginalString);
        samlError.Destination.ShouldBe(Data.AcsUrl.OriginalString);
    }

    /// <summary>
    /// Asserts that the response renders the local error page and does NOT produce
    /// an auto-submitting form that would POST to the attacker-controlled ACS URL.
    /// </summary>
    private async Task AssertErrorPageNotAttackerUrl(HttpResponseMessage result)
    {
        result.StatusCode.ShouldBe(HttpStatusCode.OK);
        result.RequestMessage.ShouldNotBeNull();

        // The response should be the error page (with errorId in query string)
        var requestUri = result.RequestMessage.RequestUri;
        requestUri.ShouldNotBeNull();

        // Verify no form action pointing to the attacker URL
        var content = await result.Content.ReadAsStringAsync(_ct);
        content.ShouldNotContain(AttackerAcsUrl);

        // Verify we're on the error page, not receiving a SAML response form
        var formActionMatch = Regex.Match(content, @"<form[^>]+action=""([^""]+)""", RegexOptions.IgnoreCase);
        if (formActionMatch.Success)
        {
            var formAction = formActionMatch.Groups[1].Value;
            formAction.ShouldNotContain("evil.example.com");
        }

        // Verify error page was rendered (errorId in the final URL)
        var errorMessage = await Fixture.GetErrorMessage(requestUri, _ct);
        errorMessage.ShouldNotBeNullOrEmpty("Expected an error page with an error message");
    }
}
