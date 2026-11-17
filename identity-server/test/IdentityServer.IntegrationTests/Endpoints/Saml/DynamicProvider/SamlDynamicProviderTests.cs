// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System.IO.Compression;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Internal.Saml.Sp;
using Microsoft.IdentityModel.Tokens;

namespace Duende.IdentityServer.IntegrationTests.Endpoints.Saml.DynamicProvider;

public class SamlDynamicProviderTests(ITestOutputHelper output)
{
    private const string Category = "Dynamic SAML provider";
    private const string SignedInUserPath = "/external-callback";
    private const string AuthorizeCallbackPath = "/connect/authorize/callback";
    private const string OtherServiceProviderAcsUrl = "https://other-sp.example.com/Saml2/Acs";

    private readonly Ct _ct = TestContext.Current.CancellationToken;

    [Fact]
    [Trait("Category", Category)]
    public async Task end_to_end_dynamic_saml_provider_federation_flow()
    {
        // Arrange
        await using var fixture = new SamlDynamicProviderFixture(output);
        await fixture.InitializeAsync();

        var clientUri = fixture.ClientHost!.Uri();

        // Act — hit the protected endpoint on Webapp 1.
        // The redirect chain is:
        //   Webapp 1 /protected → OIDC challenge → Webapp 2 authorize
        //   → dynamic SAML challenge → Webapp 3 /Saml2/SSO
        //   → unauthenticated → /auto-login → signs in test user → back to SAML callback
        //   → SAML response POST → Webapp 2 ACS → OIDC callback → Webapp 1 /protected
        var response = await fixture.FollowRedirectChainAsync($"{clientUri}/protected");

        // Assert — the final response should be 200 OK with the test user's claims
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync(_ct);
        body.ShouldContain(SamlDynamicProviderFixture.TestUserSub);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task when_mallory_posts_alices_idp_response_answering_her_own_request_expect_mallory_not_to_be_signed_in()
    {
        await using var fixture = await CreateFixtureAsync();
        var alice = await GetAliceIdpResponseAsync(fixture);
        using var mallory = fixture.SpHost!.CreateClient(allowAutoRedirect: false);
        var (malloryAuthnRequestId, malloryRelayState) = await StartSignInAsync(mallory);
        var alicesResponseAnsweringMallorysRequest = AnsweringRequest(alice.ResponseXml, malloryAuthnRequestId);

        (await PostToAcsAsync(mallory, alice.AcsUrl, alicesResponseAnsweringMallorysRequest, malloryRelayState)).Dispose();

        var mallorysSignIn = await mallory.GetAsync(SignedInUserPath, _ct);
        mallorysSignIn.StatusCode.ShouldBe(HttpStatusCode.Unauthorized,
            "an assertion confirmed for Alice's request must not sign in Mallory, whose request it does not answer");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task when_the_hosts_clock_has_passed_the_subject_confirmation_expect_the_sign_in_to_be_rejected()
    {
        await using var fixture = await CreateFixtureAsync();
        var alice = await GetAliceIdpResponseAsync(fixture);
        using var aliceBrowser = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, CookieContainer = fixture.BrowserCookies! });
        fixture.Clock!.Advance(new SamlOptions().DefaultAssertionLifetime + TokenValidationParameters.DefaultClockSkew + TimeSpan.FromMinutes(1));

        (await PostToAcsAsync(aliceBrowser, alice.AcsUrl, alice.ResponseXml, alice.RelayState)).Dispose();

        var alicesSignIn = await aliceBrowser.GetAsync($"{fixture.SpHost!.Uri()}{SignedInUserPath}", _ct);
        alicesSignIn.StatusCode.ShouldBe(HttpStatusCode.Unauthorized,
            "an assertion whose subject confirmation has expired on the host's clock must not sign anyone in");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task when_the_subject_confirmation_is_for_another_acs_expect_the_sign_in_to_be_rejected()
    {
        await using var fixture = await CreateFixtureAsync();
        using var aliceBrowser = fixture.SpHost!.CreateClient(allowAutoRedirect: false);
        var (aliceAuthnRequestId, aliceRelayState) = await StartSignInAsync(aliceBrowser);
        var acsUrl = $"{fixture.SpHost.Uri()}/federation/saml-idp/Saml2/Acs";
        var idpEntityId = $"{fixture.IdpHost!.Uri()}/Saml2";
        var now = DateTime.UtcNow;
        var responseConfirmedForOtherServiceProvider = XmlHelpers.XmlDocumentFromString($"""
                <samlp:Response xmlns:samlp="urn:oasis:names:tc:SAML:2.0:protocol" xmlns:saml="urn:oasis:names:tc:SAML:2.0:assertion"
                                ID="_{Guid.NewGuid():N}" Version="2.0" IssueInstant="{now:yyyy-MM-ddTHH:mm:ssZ}"
                                Destination="{acsUrl}" InResponseTo="{aliceAuthnRequestId}">
                  <saml:Issuer>{idpEntityId}</saml:Issuer>
                  <samlp:Status><samlp:StatusCode Value="urn:oasis:names:tc:SAML:2.0:status:Success" /></samlp:Status>
                  <saml:Assertion ID="_{Guid.NewGuid():N}" Version="2.0" IssueInstant="{now:yyyy-MM-ddTHH:mm:ssZ}">
                    <saml:Issuer>{idpEntityId}</saml:Issuer>
                    <saml:Subject>
                      <saml:NameID>alice</saml:NameID>
                      <saml:SubjectConfirmation Method="urn:oasis:names:tc:SAML:2.0:cm:bearer">
                        <saml:SubjectConfirmationData Recipient="{OtherServiceProviderAcsUrl}" NotOnOrAfter="{now.AddMinutes(5):yyyy-MM-ddTHH:mm:ssZ}" InResponseTo="{aliceAuthnRequestId}" />
                      </saml:SubjectConfirmation>
                    </saml:Subject>
                    <saml:Conditions NotBefore="{now.AddMinutes(-1):yyyy-MM-ddTHH:mm:ssZ}" NotOnOrAfter="{now.AddMinutes(30):yyyy-MM-ddTHH:mm:ssZ}">
                      <saml:AudienceRestriction><saml:Audience>{fixture.SpHost.Uri()}</saml:Audience></saml:AudienceRestriction>
                    </saml:Conditions>
                    <saml:AuthnStatement AuthnInstant="{now:yyyy-MM-ddTHH:mm:ssZ}">
                      <saml:AuthnContext><saml:AuthnContextClassRef>urn:oasis:names:tc:SAML:2.0:ac:classes:Password</saml:AuthnContextClassRef></saml:AuthnContext>
                    </saml:AuthnStatement>
                  </saml:Assertion>
                </samlp:Response>
                """);
        using var idpSigningCertificate = X509CertificateLoader.LoadPkcs12(Convert.FromBase64String(SamlFixture.StableSigningCert), null);
        ((XmlElement)responseConfirmedForOtherServiceProvider.GetElementsByTagName("Assertion", "urn:oasis:names:tc:SAML:2.0:assertion")[0]!).Sign(idpSigningCertificate, false);

        (await PostToAcsAsync(aliceBrowser, acsUrl, responseConfirmedForOtherServiceProvider.OuterXml, aliceRelayState)).Dispose();

        var alicesSignIn = await aliceBrowser.GetAsync(SignedInUserPath, _ct);
        alicesSignIn.StatusCode.ShouldBe(HttpStatusCode.Unauthorized,
            "an assertion confirmed for another service provider's ACS must not sign anyone in here");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task when_mallorys_rejected_swap_comes_before_alices_own_post_expect_alice_to_be_signed_in()
    {
        await using var fixture = await CreateFixtureAsync();
        var alice = await GetAliceIdpResponseAsync(fixture);
        using var mallory = fixture.SpHost!.CreateClient(allowAutoRedirect: false);
        var (malloryAuthnRequestId, malloryRelayState) = await StartSignInAsync(mallory);
        (await PostToAcsAsync(mallory, alice.AcsUrl, AnsweringRequest(alice.ResponseXml, malloryAuthnRequestId), malloryRelayState)).Dispose();

        using var alicesOwnPost = await PostToAcsAsync(fixture.BrowserClient!, alice.AcsUrl, alice.ResponseXml, alice.RelayState);

        alicesOwnPost.RequestMessage!.RequestUri!.AbsolutePath.ShouldBe(AuthorizeCallbackPath,
            "a forged response that was rejected must not use up Alice's assertion, so her own sign-in still completes");
    }

    private sealed record IdpResponse(string ResponseXml, string RelayState, string AcsUrl);

    private async Task<SamlDynamicProviderFixture> CreateFixtureAsync()
    {
        var fixture = new SamlDynamicProviderFixture(output,
            configureSamlProvider: provider => provider.AllowUnsolicitedAuthnResponse = false);
        await fixture.InitializeAsync();
        return fixture;
    }

    private async Task<IdpResponse> GetAliceIdpResponseAsync(SamlDynamicProviderFixture fixture)
    {
        using var idpAutoPostForm = await fixture.BrowserClient!.GetAsync($"{fixture.ClientHost!.Uri()}/protected", _ct);
        var (responseXml, relayState, acsUrl) = await SamlTestHelpers.ExtractSamlResponse(idpAutoPostForm, _ct);
        relayState.ShouldNotBeNull("the IdP's form for Alice carries the SP's RelayState");
        return new IdpResponse(responseXml, relayState, acsUrl);
    }

    private async Task<(string AuthnRequestId, string RelayState)> StartSignInAsync(HttpClient browser)
    {
        using var redirectToIdp = await browser.GetAsync($"/account/login?ReturnUrl={Uri.EscapeDataString(SignedInUserPath)}", _ct);
        var query = redirectToIdp.Headers.Location!.Query;

        var deflatedAuthnRequest = Convert.FromBase64String(Uri.UnescapeDataString(SamlTestHelpers.ExtractRawQueryParam(query, "SAMLRequest")!));
        await using var inflater = new DeflateStream(new MemoryStream(deflatedAuthnRequest), CompressionMode.Decompress);
        using var reader = new StreamReader(inflater, Encoding.UTF8);
        var authnRequest = XElement.Parse(await reader.ReadToEndAsync(_ct));

        return (authnRequest.Attribute("ID")!.Value, Uri.UnescapeDataString(SamlTestHelpers.ExtractRawQueryParam(query, "RelayState")!));
    }

    private static string AnsweringRequest(string idpResponseXml, string authnRequestId) =>
        new Regex("InResponseTo=\"[^\"]*\"").Replace(idpResponseXml, $"InResponseTo=\"{authnRequestId}\"", 1);

    private async Task<HttpResponseMessage> PostToAcsAsync(HttpClient browser, string acsUrl, string responseXml, string relayState)
    {
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["SAMLResponse"] = SamlTestHelpers.ConvertToBase64Encoded(responseXml),
            ["RelayState"] = relayState
        });

        return await browser.PostAsync(acsUrl, form, _ct);
    }
}
