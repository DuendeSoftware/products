// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using System.Collections.ObjectModel;
using System.Net;
using System.Security.Claims;
using System.Web;
using Duende.IdentityModel;
using Duende.IdentityServer.IntegrationTests.Common;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Saml;
using static Duende.IdentityServer.IntegrationTests.Endpoints.Saml.SamlTestHelpers;

namespace Duende.IdentityServer.IntegrationTests.Endpoints.Saml;

public class SamlSigninCallbackEndpointTests
{
    private const string Category = "SAML Signin Callback Endpoint";

    private readonly Ct _ct = TestContext.Current.CancellationToken;

    private SamlFixture Fixture = new();

    private SamlData Data => Fixture.Data;

    private SamlDataBuilder Build => Fixture.Builder;

    [Fact]
    [Trait("Category", Category)]
    public async Task callback_returns_error_when_state_not_found()
    {
        Fixture.ServiceProviders.Add(Build.SamlServiceProvider());
        await Fixture.InitializeAsync();

        var stateId = await InitiateFlowAndExtractStateId();

        Fixture.UserToSignIn =
            new ClaimsPrincipal(new ClaimsIdentity([new Claim(JwtClaimTypes.Subject, "user-id")], "Test"));
        await Fixture.NonRedirectingClient.GetAsync("/__signin", _ct);

        // Remove state so the next request has a state id that points to non-existent state
        var samlSigninStateStore = Fixture.Get<ISamlSigninStateStore>();
        await samlSigninStateStore.RemoveSigninRequestStateAsync(Guid.Parse(stateId), _ct);

        var result = await Fixture.NonRedirectingClient.GetAsync($"/Saml2/SSO/Callback?samlStateId={stateId}", _ct);

        var errorMessage = await Fixture.GetErrorFromRedirect(result, _ct);
        errorMessage.ShouldBe("SAML authentication state not found or expired");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task callback_returns_error_when_state_id_is_missing()
    {
        Fixture.ServiceProviders.Add(Build.SamlServiceProvider());
        await Fixture.InitializeAsync();

        Fixture.UserToSignIn =
            new ClaimsPrincipal(new ClaimsIdentity([new Claim(JwtClaimTypes.Subject, "user-id")], "Test"));
        await Fixture.Client.GetAsync("/__signin", _ct);

        // Do not make request to the sign-in endpoint first so no state id is created
        var result = await Fixture.Client.GetAsync("/Saml2/SSO/Callback", _ct);

        result.StatusCode.ShouldBe(HttpStatusCode.OK);
        var errorMessage = await Fixture.GetErrorMessage(result.RequestMessage?.RequestUri, _ct);
        errorMessage.ShouldBe("Missing or invalid SAML state identifier");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task callback_redirects_to_login_when_user_not_authenticated()
    {
        Fixture.ServiceProviders.Add(Build.SamlServiceProvider());
        await Fixture.InitializeAsync();

        var stateId = await InitiateFlowAndExtractStateId();

        // Do not sign in — the callback should redirect to the login page
        var result = await Fixture.NonRedirectingClient.GetAsync($"/Saml2/SSO/Callback?samlStateId={stateId}", _ct);

        result.StatusCode.ShouldBe(HttpStatusCode.Found);
        var location = result.Headers.Location;
        location.ShouldNotBeNull();

        var resolved = new Uri(new Uri(IdentityServerPipeline.BaseUrl), location);
        resolved.AbsolutePath.ShouldBe(Fixture.LoginUrl.ToString());

        var returnUrl = HttpUtility.ParseQueryString(resolved.Query)["ReturnUrl"];
        returnUrl.ShouldNotBeNull();

        var returnUri = new Uri(new Uri(IdentityServerPipeline.BaseUrl), returnUrl);
        returnUri.AbsolutePath.ShouldBe("/Saml2/SSO/Callback");
        HttpUtility.ParseQueryString(returnUri.Query)["samlStateId"].ShouldBe(stateId);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task callback_returns_error_when_service_provider_not_found()
    {
        var sp = Build.SamlServiceProvider();
        Fixture.ServiceProviders.Add(sp);
        await Fixture.InitializeAsync();

        var stateId = await InitiateFlowAndExtractStateId();

        Fixture.UserToSignIn =
            new ClaimsPrincipal(new ClaimsIdentity([new Claim(JwtClaimTypes.Subject, "user-id")], "Test"));
        await Fixture.NonRedirectingClient.GetAsync("/__signin", _ct);

        Fixture.ClearServiceProvidersAsync();

        var result = await Fixture.NonRedirectingClient.GetAsync($"/Saml2/SSO/Callback?samlStateId={stateId}", _ct);

        var errorMessage = await Fixture.GetErrorFromRedirect(result, _ct);
        errorMessage.ShouldBe("Service provider not found");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task callback_returns_error_when_service_provider_disabled()
    {
        var sp = Build.SamlServiceProvider();
        Fixture.ServiceProviders.Add(sp);
        await Fixture.InitializeAsync();

        var stateId = await InitiateFlowAndExtractStateId();

        Fixture.UserToSignIn =
            new ClaimsPrincipal(new ClaimsIdentity([new Claim(JwtClaimTypes.Subject, "user-id")], "Test"));
        await Fixture.NonRedirectingClient.GetAsync("/__signin", _ct);

        // Disabling the SP causes InMemorySamlServiceProviderStore.FindByEntityIdAsync
        // to return null (it filters by Enabled), so the endpoint reports "not found".
        sp.Enabled = false;

        var result = await Fixture.NonRedirectingClient.GetAsync($"/Saml2/SSO/Callback?samlStateId={stateId}", _ct);

        var errorMessage = await Fixture.GetErrorFromRedirect(result, _ct);
        errorMessage.ShouldBe("Service provider not found");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task callback_state_survives_after_successful_login_allowing_retries()
    {
        Fixture.ServiceProviders.Add(Build.SamlServiceProvider());
        await Fixture.InitializeAsync();

        var stateId = await InitiateFlowAndExtractStateId();

        Fixture.UserToSignIn =
            new ClaimsPrincipal(new ClaimsIdentity([new Claim(JwtClaimTypes.Subject, "user-id")], "Test"));
        await Fixture.NonRedirectingClient.GetAsync("/__signin", _ct);

        var firstResult = await Fixture.NonRedirectingClient.GetAsync($"/Saml2/SSO/Callback?samlStateId={stateId}", _ct);

        // First use succeeds
        firstResult.StatusCode.ShouldBe(HttpStatusCode.OK);
        var html = await firstResult.Content.ReadAsStringAsync(_ct);
        html.ShouldContain("SAMLResponse");

        // Second callback with same stateId succeeds too — state is intentionally
        // not deleted after use. The TTL handles cleanup, and leaving state alive
        // allows the user to retry (e.g., browser reload) if the response doesn't
        // reach the SP on the first attempt.
        var secondResult = await Fixture.NonRedirectingClient.GetAsync($"/Saml2/SSO/Callback?samlStateId={stateId}", _ct);

        secondResult.StatusCode.ShouldBe(HttpStatusCode.OK);
        var secondHtml = await secondResult.Content.ReadAsStringAsync(_ct);
        secondHtml.ShouldContain("SAMLResponse");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task callback_returns_error_when_state_is_expired()
    {
        Fixture.ServiceProviders.Add(Build.SamlServiceProvider());
        await Fixture.InitializeAsync();

        var stateId = await InitiateFlowAndExtractStateId();

        Fixture.UserToSignIn =
            new ClaimsPrincipal(new ClaimsIdentity([new Claim(JwtClaimTypes.Subject, "user-id")], "Test"));
        await Fixture.NonRedirectingClient.GetAsync("/__signin", _ct);

        // Advance time past the state TTL so the stored state expires
        Fixture.Data.FakeTimeProvider.Advance(TimeSpan.FromMinutes(16));

        var result = await Fixture.NonRedirectingClient.GetAsync($"/Saml2/SSO/Callback?samlStateId={stateId}", _ct);

        var errorMessage = await Fixture.GetErrorFromRedirect(result, _ct);
        errorMessage.ShouldBe("SAML authentication state not found or expired");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task state_contains_correct_request_information()
    {
        Fixture.ServiceProviders.Add(Build.SamlServiceProvider());
        await Fixture.InitializeAsync();

        var specificRelayState = "test-relay-state-123";
        var stateId = await InitiateFlowAndExtractStateId(relayState: specificRelayState);

        Fixture.UserToSignIn =
            new ClaimsPrincipal(new ClaimsIdentity([new Claim(JwtClaimTypes.Subject, "user-id")], "Test"));
        await Fixture.NonRedirectingClient.GetAsync("/__signin", _ct);

        var result = await Fixture.NonRedirectingClient.GetAsync($"/Saml2/SSO/Callback?samlStateId={stateId}", _ct);

        result.StatusCode.ShouldBe(HttpStatusCode.OK);
        var samlResponse = await ExtractSamlSuccessFromPostAsync(result, _ct);
        samlResponse.ShouldNotBeNull();
        samlResponse.RelayState.ShouldBe(specificRelayState);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task callback_returns_signed_response_when_sign_response_configured()
    {
        var sp = Build.SamlServiceProvider();
        sp.SigningBehavior = SamlSigningBehavior.SignResponse;
        Fixture.ServiceProviders.Add(sp);
        await Fixture.InitializeAsync();

        var stateId = await InitiateFlowAndExtractStateId();

        Fixture.UserToSignIn =
            new ClaimsPrincipal(new ClaimsIdentity([new Claim(JwtClaimTypes.Subject, "user-id")], "Test"));
        await Fixture.NonRedirectingClient.GetAsync("/__signin", _ct);

        var result = await Fixture.NonRedirectingClient.GetAsync($"/Saml2/SSO/Callback?samlStateId={stateId}", _ct);

        result.StatusCode.ShouldBe(HttpStatusCode.OK);
        var (responseXml, _, _) = await ExtractSamlResponse(result, _ct);

        VerifySignaturePresence(responseXml, expectResponseSignature: true, expectAssertionSignature: false);

        var successResponse = await ExtractSamlSuccessFromPostAsync(result, _ct);
        successResponse.StatusCode.ShouldBe("urn:oasis:names:tc:SAML:2.0:status:Success");
        successResponse.Assertion.Subject?.NameId.ShouldBe("user-id");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task callback_returns_signed_assertion_when_sign_assertion_configured()
    {
        var sp = Build.SamlServiceProvider();
        sp.SigningBehavior = SamlSigningBehavior.SignAssertion;
        Fixture.ServiceProviders.Add(sp);
        await Fixture.InitializeAsync();

        var stateId = await InitiateFlowAndExtractStateId();

        Fixture.UserToSignIn =
            new ClaimsPrincipal(new ClaimsIdentity([new Claim(JwtClaimTypes.Subject, "user-id")], "Test"));
        await Fixture.NonRedirectingClient.GetAsync("/__signin", _ct);

        var result = await Fixture.NonRedirectingClient.GetAsync($"/Saml2/SSO/Callback?samlStateId={stateId}", _ct);

        result.StatusCode.ShouldBe(HttpStatusCode.OK);
        var (responseXml, _, _) = await ExtractSamlResponse(result, _ct);

        VerifySignaturePresence(responseXml, expectResponseSignature: false, expectAssertionSignature: true);

        var successResponse = await ExtractSamlSuccessFromPostAsync(result, _ct);
        successResponse.StatusCode.ShouldBe("urn:oasis:names:tc:SAML:2.0:status:Success");
        successResponse.Assertion.Subject?.NameId.ShouldBe("user-id");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task callback_returns_unsigned_response_when_do_not_sign_configured()
    {
        var sp = Build.SamlServiceProvider();
        sp.SigningBehavior = SamlSigningBehavior.DoNotSign;
        Fixture.ServiceProviders.Add(sp);
        await Fixture.InitializeAsync();

        var stateId = await InitiateFlowAndExtractStateId();

        Fixture.UserToSignIn =
            new ClaimsPrincipal(new ClaimsIdentity([new Claim(JwtClaimTypes.Subject, "user-id")], "Test"));
        await Fixture.NonRedirectingClient.GetAsync("/__signin", _ct);

        var result = await Fixture.NonRedirectingClient.GetAsync($"/Saml2/SSO/Callback?samlStateId={stateId}", _ct);

        result.StatusCode.ShouldBe(HttpStatusCode.OK);
        var (responseXml, _, _) = await ExtractSamlResponse(result, _ct);

        VerifySignaturePresence(responseXml, expectResponseSignature: false, expectAssertionSignature: false);

        var successResponse = await ExtractSamlSuccessFromPostAsync(result, _ct);
        successResponse.StatusCode.ShouldBe("urn:oasis:names:tc:SAML:2.0:status:Success");
        successResponse.Assertion.Subject?.NameId.ShouldBe("user-id");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task callback_signature_works_with_maximum_attribute_payload()
    {
        var sp = Build.SamlServiceProvider();
        sp.SigningBehavior = SamlSigningBehavior.SignBoth;
        sp.ClaimMappings = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>
        {
            [JwtClaimTypes.Subject] = "sub",
            [JwtClaimTypes.Name] = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name",
            [JwtClaimTypes.Email] = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress",
            [JwtClaimTypes.Role] = "http://schemas.xmlsoap.org/ws/2005/05/identity/role",
            ["department"] = "ou",
            ["location"] = "loc",
            ["employee_id"] = "emp_id",
            ["manager"] = "manager",
            ["cost_center"] = "cc"
        });
        sp.RequestedClaimTypes = [JwtClaimTypes.Subject, JwtClaimTypes.Name, JwtClaimTypes.Email, JwtClaimTypes.Role, "department", "location", "employee_id", "manager", "cost_center"];
        Fixture.ServiceProviders.Add(sp);
        await Fixture.InitializeAsync();

        var stateId = await InitiateFlowAndExtractStateId();

        // Create user with many claims
        var claims = new List<Claim>
        {
            new Claim(JwtClaimTypes.Subject, "user-id"),
            new Claim(JwtClaimTypes.Name, "Test User"),
            new Claim(JwtClaimTypes.Email, "test@example.com"),
            new Claim(JwtClaimTypes.Role, "Admin"),
            new Claim(JwtClaimTypes.Role, "User"),
            new Claim("department", "Engineering"),
            new Claim("location", "Seattle"),
            new Claim("employee_id", "EMP12345"),
            new Claim("manager", "manager@example.com"),
            new Claim("cost_center", "CC-1234")
        };

        Fixture.UserToSignIn = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
        await Fixture.NonRedirectingClient.GetAsync("/__signin", _ct);

        var result = await Fixture.NonRedirectingClient.GetAsync($"/Saml2/SSO/Callback?samlStateId={stateId}", _ct);

        result.StatusCode.ShouldBe(HttpStatusCode.OK);
        var (responseXml, _, _) = await ExtractSamlResponse(result, _ct);

        VerifySignaturePresence(responseXml, expectResponseSignature: true, expectAssertionSignature: true);

        var successResponse = await ExtractSamlSuccessFromPostAsync(result, _ct);
        successResponse.StatusCode.ShouldBe("urn:oasis:names:tc:SAML:2.0:status:Success");
        successResponse.Assertion.Attributes.ShouldNotBeNull();
        successResponse.Assertion.Attributes!.Count.ShouldBeGreaterThan(4); // At least the claims we added
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task callback_signature_preserves_xml_special_characters_in_attributes()
    {
        var sp = Build.SamlServiceProvider();
        sp.SigningBehavior = SamlSigningBehavior.SignAssertion;
        sp.RequestedClaimTypes = [JwtClaimTypes.Name, "description", "xml_data"];
        sp.ClaimMappings = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>
        {
            [JwtClaimTypes.Name] = JwtClaimTypes.Name,
            ["description"] = "description",
            ["xml_data"] = "xml_data"
        });
        Fixture.ServiceProviders.Add(sp);
        await Fixture.InitializeAsync();

        var stateId = await InitiateFlowAndExtractStateId();

        var claims = new List<Claim>
        {
            new Claim(JwtClaimTypes.Subject, "user-id"),
            new Claim(JwtClaimTypes.Name, "Test <User> & \"Company\""),
            new Claim("description", "Value with <tags> & \"quotes\" and 'apostrophes'"),
            new Claim("xml_data", "<root><element attr=\"value\">text</element></root>")
        };

        Fixture.UserToSignIn = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
        await Fixture.NonRedirectingClient.GetAsync("/__signin", _ct);

        var result = await Fixture.NonRedirectingClient.GetAsync($"/Saml2/SSO/Callback?samlStateId={stateId}", _ct);

        result.StatusCode.ShouldBe(HttpStatusCode.OK);
        var (responseXml, _, _) = await ExtractSamlResponse(result, _ct);

        VerifySignaturePresence(responseXml, expectResponseSignature: false, expectAssertionSignature: true);

        var (_, _, responseElement) = ParseSamlResponseXml(responseXml);
        responseElement.ShouldNotBeNull();

        var successResponse = await ExtractSamlSuccessFromPostAsync(result, _ct);
        successResponse.StatusCode.ShouldBe("urn:oasis:names:tc:SAML:2.0:status:Success");

        var nameAttribute = successResponse.Assertion.Attributes?.FirstOrDefault(a => a.Name == JwtClaimTypes.Name);
        nameAttribute.ShouldNotBeNull();
        nameAttribute.Value.ShouldBe("Test <User> & \"Company\"");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task callback_redirects_to_login_when_force_authn_and_user_did_not_reauthenticate()
    {
        Fixture.ServiceProviders.Add(Build.SamlServiceProvider());
        await Fixture.InitializeAsync();

        // Sign in user at T0
        Fixture.UserToSignIn =
            new ClaimsPrincipal(new ClaimsIdentity([new Claim(JwtClaimTypes.Subject, "user-id")], "Test"));
        await Fixture.NonRedirectingClient.GetAsync("/__signin", _ct);

        // Advance time so the ForceAuthn request's state.CreatedUtc > auth_time
        Fixture.Data.FakeTimeProvider.Advance(TimeSpan.FromMinutes(1));

        // Initiate ForceAuthn flow — user is already authenticated but ForceAuthn
        // should require re-authentication
        var stateId = await InitiateForceAuthnFlowAndExtractStateId();

        // Hit callback WITHOUT re-authenticating — auth_time is still T0,
        // but state.CreatedUtc is T0+1min, so the check should fail
        var result = await Fixture.NonRedirectingClient.GetAsync($"/Saml2/SSO/Callback?samlStateId={stateId}", _ct);

        result.StatusCode.ShouldBe(HttpStatusCode.Found);
        var location = result.Headers.Location;
        location.ShouldNotBeNull();

        var resolved = new Uri(new Uri(IdentityServerPipeline.BaseUrl), location);
        resolved.AbsolutePath.ShouldBe(Fixture.LoginUrl.ToString());
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task callback_succeeds_when_force_authn_and_user_reauthenticated()
    {
        Fixture.ServiceProviders.Add(Build.SamlServiceProvider());
        await Fixture.InitializeAsync();

        // Sign in user at T0
        Fixture.UserToSignIn =
            new ClaimsPrincipal(new ClaimsIdentity([new Claim(JwtClaimTypes.Subject, "user-id")], "Test"));
        await Fixture.NonRedirectingClient.GetAsync("/__signin", _ct);

        // Advance time so the ForceAuthn request's state.CreatedUtc > auth_time
        Fixture.Data.FakeTimeProvider.Advance(TimeSpan.FromMinutes(1));

        var stateId = await InitiateForceAuthnFlowAndExtractStateId();

        // Re-authenticate (simulates user completing login page)
        Fixture.Data.FakeTimeProvider.Advance(TimeSpan.FromSeconds(5));
        Fixture.UserToSignIn =
            new ClaimsPrincipal(new ClaimsIdentity([new Claim(JwtClaimTypes.Subject, "user-id")], "Test"));
        await Fixture.NonRedirectingClient.GetAsync("/__signin", _ct);

        // Now callback should succeed — auth_time is after state.CreatedUtc
        var result = await Fixture.NonRedirectingClient.GetAsync($"/Saml2/SSO/Callback?samlStateId={stateId}", _ct);

        result.StatusCode.ShouldBe(HttpStatusCode.OK);
        var html = await result.Content.ReadAsStringAsync(_ct);
        html.ShouldContain("SAMLResponse");
    }

    private async Task<string> InitiateForceAuthnFlowAndExtractStateId()
    {
        var authnRequestXml = Build.AuthNRequestXml(forceAuthn: true);
        var urlEncoded = await EncodeRequest(authnRequestXml, _ct);
        var url = $"/Saml2/SSO?SAMLRequest={urlEncoded}";

        var ssoResult = await Fixture.NonRedirectingClient.GetAsync(url, _ct);

        ssoResult.StatusCode.ShouldBe(HttpStatusCode.SeeOther);

        var stateId = ExtractStateIdFromReturnUrl(ssoResult);
        stateId.ShouldNotBeNullOrEmpty("samlStateId should be present in the ReturnUrl of the SSO redirect");
        return stateId!;
    }

    private async Task<string> InitiateFlowAndExtractStateId(string? relayState = null)
    {
        var authnRequestXml = Build.AuthNRequestXml();
        var urlEncoded = await EncodeRequest(authnRequestXml, _ct);
        var url = $"/Saml2/SSO?SAMLRequest={urlEncoded}";
        if (relayState != null)
        {
            url += $"&RelayState={relayState}";
        }

        var ssoResult = await Fixture.NonRedirectingClient.GetAsync(url, _ct);

        ssoResult.StatusCode.ShouldBe(HttpStatusCode.SeeOther);

        var stateId = ExtractStateIdFromReturnUrl(ssoResult);
        stateId.ShouldNotBeNullOrEmpty("samlStateId should be present in the ReturnUrl of the SSO redirect");
        return stateId!;
    }
}
