// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Collections.ObjectModel;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Web;
using Duende.IdentityModel;
using Duende.IdentityServer.Internal.Saml.SingleSignin;
using Duende.IdentityServer.Models;
using Microsoft.AspNetCore.Mvc;
using static Duende.IdentityServer.IntegrationTests.Endpoints.Saml.SamlTestHelpers;
using StateId = Duende.IdentityServer.Internal.Saml.SingleSignin.Models.StateId;

namespace Duende.IdentityServer.IntegrationTests.Endpoints.Saml;

public class SamlSigninCallbackEndpointTests
{
    private const string Category = "SAML Signin Callback Endpoint";

    private SamlFixture Fixture = new();

    private SamlData Data => Fixture.Data;

    private SamlDataBuilder Build => Fixture.Builder;

    [Fact]
    [Trait("Category", Category)]
    public async Task callback_returns_error_when_state_not_found()
    {
        Fixture.ServiceProviders.Add(Build.SamlServiceProvider());
        await Fixture.InitializeAsync();

        Fixture.UserToSignIn =
            new ClaimsPrincipal(new ClaimsIdentity([new Claim(JwtClaimTypes.Subject, "user-id")], "Test"));
        await Fixture.Client.GetAsync("/__signin", CancellationToken.None);

        var authnRequestXml = Build.AuthNRequestXml();
        var urlEncoded = await EncodeRequest(authnRequestXml, CancellationToken.None);
        var signinResult = await Fixture.NonRedirectingClient.GetAsync($"/saml/signin?SAMLRequest={urlEncoded}", CancellationToken.None);

        signinResult.StatusCode.ShouldBe(HttpStatusCode.Found);
        signinResult.Headers.Location.ShouldNotBeNull();

        var stateId = ExtractStateIdFromCookie(signinResult);
        stateId.ShouldNotBeNull();

        // Remove state from store so the next request is sent with a state id that for state which no longer exists
        var samlSigninStateStore = Fixture.Get<ISamlSigninStateStore>();
        await samlSigninStateStore.RetrieveSigninRequestStateAsync(new StateId(Guid.Parse(stateId)), CancellationToken.None);

        var result = await Fixture.NonRedirectingClient.GetAsync("/saml/signin_callback", CancellationToken.None);

        result.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problemDetails = await result.Content.ReadFromJsonAsync<ProblemDetails>(CancellationToken.None);
        problemDetails.ShouldNotBeNull();
        problemDetails.Detail.ShouldBe($"The request {stateId} could not be found.");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task callback_returns_error_when_state_id_is_missing()
    {
        Fixture.ServiceProviders.Add(Build.SamlServiceProvider());
        await Fixture.InitializeAsync();

        Fixture.UserToSignIn =
            new ClaimsPrincipal(new ClaimsIdentity([new Claim(JwtClaimTypes.Subject, "user-id")], "Test"));
        await Fixture.Client.GetAsync("/__signin", CancellationToken.None);

        // Do not make request to the sign-in endpoint first so no state id is created

        var result = await Fixture.Client.GetAsync("/saml/signin_callback", CancellationToken.None);

        result.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problemDetails = await result.Content.ReadFromJsonAsync<ProblemDetails>(CancellationToken.None);
        problemDetails.ShouldNotBeNull();
        problemDetails.Detail.ShouldBe("No state id could be found.");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task callback_redirects_to_login_when_user_not_authenticated_and_state_is_found()
    {
        Fixture.ServiceProviders.Add(Build.SamlServiceProvider());
        await Fixture.InitializeAsync();

        Fixture.UserToSignIn =
            new ClaimsPrincipal(new ClaimsIdentity([new Claim(JwtClaimTypes.Subject, "user-id")], "Test"));
        await Fixture.NonRedirectingClient.GetAsync("/__signin", CancellationToken.None);

        var authnRequestXml = Build.AuthNRequestXml();
        var urlEncoded = await EncodeRequest(authnRequestXml, CancellationToken.None);
        var signinResult = await Fixture.NonRedirectingClient.GetAsync($"/saml/signin?SAMLRequest={urlEncoded}", CancellationToken.None);

        signinResult.StatusCode.ShouldBe(HttpStatusCode.Found);
        var redirectUri = signinResult.Headers.Location;
        redirectUri.ShouldNotBeNull();
        redirectUri.ToString().ShouldStartWith("/saml/signin_callback");

        await Fixture.NonRedirectingClient.GetAsync("/__signout", CancellationToken.None);

        var result = await Fixture.NonRedirectingClient.GetAsync($"/saml/signin_callback", CancellationToken.None);

        result.StatusCode.ShouldBe(HttpStatusCode.Found);
        var resultRedirectUri = result.Headers.Location;
        resultRedirectUri.ShouldNotBeNull();
        HttpUtility.UrlDecode(resultRedirectUri.ToString()).ShouldBe($"{Fixture.LoginUrl}?ReturnUrl={Fixture.SignInCallbackUrl}");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task callback_returns_error_when_service_provider_not_found()
    {
        var sp = Build.SamlServiceProvider();
        Fixture.ServiceProviders.Add(sp);
        await Fixture.InitializeAsync();

        Fixture.UserToSignIn =
            new ClaimsPrincipal(new ClaimsIdentity([new Claim(JwtClaimTypes.Subject, "user-id")], "Test"));
        await Fixture.NonRedirectingClient.GetAsync("/__signin", CancellationToken.None);

        var authnRequestXml = Build.AuthNRequestXml();
        var urlEncoded = await EncodeRequest(authnRequestXml, CancellationToken.None);
        var signinResult = await Fixture.NonRedirectingClient.GetAsync($"/saml/signin?SAMLRequest={urlEncoded}", CancellationToken.None);

        signinResult.StatusCode.ShouldBe(HttpStatusCode.Found);
        var redirectUri = signinResult.Headers.Location;
        redirectUri.ShouldNotBeNull();

        Fixture.ClearServiceProvidersAsync();

        var result = await Fixture.NonRedirectingClient.GetAsync("/saml/signin_callback", CancellationToken.None);

        result.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problemDetails = await result.Content.ReadFromJsonAsync<ProblemDetails>(CancellationToken.None);
        problemDetails.ShouldNotBeNull();
        problemDetails.Detail.ShouldBe($"Service Provider '{sp.EntityId}' is not registered or is disabled");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task callback_returns_error_when_service_provider_disabled()
    {
        var sp = Build.SamlServiceProvider();
        Fixture.ServiceProviders.Add(sp);
        await Fixture.InitializeAsync();

        Fixture.UserToSignIn =
            new ClaimsPrincipal(new ClaimsIdentity([new Claim(JwtClaimTypes.Subject, "user-id")], "Test"));
        await Fixture.NonRedirectingClient.GetAsync("/__signin", CancellationToken.None);

        var authnRequestXml = Build.AuthNRequestXml();
        var urlEncoded = await EncodeRequest(authnRequestXml, CancellationToken.None);
        var signinResult = await Fixture.NonRedirectingClient.GetAsync($"/saml/signin?SAMLRequest={urlEncoded}", CancellationToken.None);

        signinResult.StatusCode.ShouldBe(HttpStatusCode.Found);
        var redirectUri = signinResult.Headers.Location;
        redirectUri.ShouldNotBeNull();

        // Ideally we would fetch the SP via a store and update it, but since the store doesn't provide that functionality
        // we'll rely on everything being in memory and holding onto a reference to the SP in the store for now
        sp.Enabled = false;

        var result = await Fixture.NonRedirectingClient.GetAsync("/saml/signin_callback", CancellationToken.None);

        result.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problemDetails = await result.Content.ReadFromJsonAsync<ProblemDetails>(CancellationToken.None);
        problemDetails.ShouldNotBeNull();
        problemDetails.Detail.ShouldBe($"Service Provider '{sp.EntityId}' is not registered or is disabled");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task callback_state_is_not_persisted_after_successful_login()
    {
        Fixture.ServiceProviders.Add(Build.SamlServiceProvider());
        await Fixture.InitializeAsync();

        Fixture.UserToSignIn =
            new ClaimsPrincipal(new ClaimsIdentity([new Claim(JwtClaimTypes.Subject, "user-id")], "Test"));
        await Fixture.NonRedirectingClient.GetAsync("/__signin", CancellationToken.None);

        var authnRequestXml = Build.AuthNRequestXml();
        var urlEncoded = await EncodeRequest(authnRequestXml, CancellationToken.None);

        var signinResult = await Fixture.NonRedirectingClient.GetAsync($"/saml/signin?SAMLRequest={urlEncoded}", CancellationToken.None);

        signinResult.StatusCode.ShouldBe(HttpStatusCode.Found);
        var redirectUri = signinResult.Headers.Location;
        redirectUri.ShouldNotBeNull();

        var firstResult = await Fixture.NonRedirectingClient.GetAsync("/saml/signin_callback", CancellationToken.None);

        // First use succeeds
        firstResult.StatusCode.ShouldBe(HttpStatusCode.OK);
        var html = await firstResult.Content.ReadAsStringAsync(CancellationToken.None);
        html.ShouldContain("SAMLResponse");

        // Second callback with same stateId (replay attack)
        var secondResult = await Fixture.NonRedirectingClient.GetAsync("/saml/signin_callback", CancellationToken.None);

        //  Second use should fail
        secondResult.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problemDetails = await secondResult.Content.ReadFromJsonAsync<ProblemDetails>(CancellationToken.None);
        problemDetails.ShouldNotBeNull();
        problemDetails.Detail.ShouldBe("No state id could be found.");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task callback_returns_error_when_state_is_expired()
    {
        Fixture.ServiceProviders.Add(Build.SamlServiceProvider());
        await Fixture.InitializeAsync();

        Fixture.UserToSignIn =
            new ClaimsPrincipal(new ClaimsIdentity([new Claim(JwtClaimTypes.Subject, "user-id")], "Test"));
        await Fixture.NonRedirectingClient.GetAsync("/__signin", CancellationToken.None);

        var authnRequestXml = Build.AuthNRequestXml();
        var urlEncoded = await EncodeRequest(authnRequestXml, CancellationToken.None);

        var signinResult = await Fixture.NonRedirectingClient.GetAsync($"/saml/signin?SAMLRequest={urlEncoded}", CancellationToken.None);

        signinResult.StatusCode.ShouldBe(HttpStatusCode.Found);
        signinResult.Headers.Location.ShouldNotBeNull();

        var stateId = ExtractStateIdFromCookie(signinResult);

        Fixture.Data.FakeTimeProvider.Advance(TimeSpan.FromMinutes(11));

        var result = await Fixture.NonRedirectingClient.GetAsync("/saml/signin_callback", CancellationToken.None);

        result.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problemDetails = await result.Content.ReadFromJsonAsync<ProblemDetails>(CancellationToken.None);
        problemDetails.ShouldNotBeNull();
        problemDetails.Detail.ShouldBe($"The request {stateId} could not be found.");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task state_contains_correct_request_information()
    {
        Fixture.ServiceProviders.Add(Build.SamlServiceProvider());
        await Fixture.InitializeAsync();

        Fixture.UserToSignIn =
            new ClaimsPrincipal(new ClaimsIdentity([new Claim(JwtClaimTypes.Subject, "user-id")], "Test"));
        await Fixture.NonRedirectingClient.GetAsync("/__signin", CancellationToken.None);

        var specificRelayState = "test-relay-state-123";
        var authnRequestXml = Build.AuthNRequestXml();
        var urlEncoded = await EncodeRequest(authnRequestXml, CancellationToken.None);

        var signinResult = await Fixture.NonRedirectingClient.GetAsync($"/saml/signin?SAMLRequest={urlEncoded}&RelayState={specificRelayState}", CancellationToken.None);

        signinResult.StatusCode.ShouldBe(HttpStatusCode.Found);
        var redirectUri = signinResult.Headers.Location;
        redirectUri.ShouldNotBeNull();

        var result = await Fixture.NonRedirectingClient.GetAsync("/saml/signin_callback", CancellationToken.None);

        result.StatusCode.ShouldBe(HttpStatusCode.OK);
        var samlResponse = await ExtractSamlSuccessFromPostAsync(result, CancellationToken.None);
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

        Fixture.UserToSignIn =
            new ClaimsPrincipal(new ClaimsIdentity([new Claim(JwtClaimTypes.Subject, "user-id")], "Test"));
        await Fixture.NonRedirectingClient.GetAsync("/__signin", CancellationToken.None);

        var authnRequestXml = Build.AuthNRequestXml();
        var urlEncoded = await EncodeRequest(authnRequestXml, CancellationToken.None);
        var signinResult = await Fixture.NonRedirectingClient.GetAsync($"/saml/signin?SAMLRequest={urlEncoded}", CancellationToken.None);

        signinResult.StatusCode.ShouldBe(HttpStatusCode.Found);
        var redirectUri = signinResult.Headers.Location;
        redirectUri.ShouldNotBeNull();

        var result = await Fixture.NonRedirectingClient.GetAsync("/saml/signin_callback", CancellationToken.None);

        result.StatusCode.ShouldBe(HttpStatusCode.OK);
        var (responseXml, _, _) = await ExtractSamlResponse(result, CancellationToken.None);

        VerifySignaturePresence(responseXml, expectResponseSignature: true, expectAssertionSignature: false);

        var successResponse = await ExtractSamlSuccessFromPostAsync(result, CancellationToken.None);
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

        Fixture.UserToSignIn =
            new ClaimsPrincipal(new ClaimsIdentity([new Claim(JwtClaimTypes.Subject, "user-id")], "Test"));
        await Fixture.NonRedirectingClient.GetAsync("/__signin", CancellationToken.None);

        var authnRequestXml = Build.AuthNRequestXml();
        var urlEncoded = await EncodeRequest(authnRequestXml, CancellationToken.None);
        var signinResult = await Fixture.NonRedirectingClient.GetAsync($"/saml/signin?SAMLRequest={urlEncoded}", CancellationToken.None);

        signinResult.StatusCode.ShouldBe(HttpStatusCode.Found);
        var redirectUri = signinResult.Headers.Location;
        redirectUri.ShouldNotBeNull();

        var result = await Fixture.NonRedirectingClient.GetAsync("/saml/signin_callback", CancellationToken.None);

        result.StatusCode.ShouldBe(HttpStatusCode.OK);
        var (responseXml, _, _) = await ExtractSamlResponse(result, CancellationToken.None);

        VerifySignaturePresence(responseXml, expectResponseSignature: false, expectAssertionSignature: true);

        var successResponse = await ExtractSamlSuccessFromPostAsync(result, CancellationToken.None);
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

        Fixture.UserToSignIn =
            new ClaimsPrincipal(new ClaimsIdentity([new Claim(JwtClaimTypes.Subject, "user-id")], "Test"));
        await Fixture.NonRedirectingClient.GetAsync("/__signin", CancellationToken.None);

        var authnRequestXml = Build.AuthNRequestXml();
        var urlEncoded = await EncodeRequest(authnRequestXml, CancellationToken.None);
        var signinResult = await Fixture.NonRedirectingClient.GetAsync($"/saml/signin?SAMLRequest={urlEncoded}", CancellationToken.None);

        signinResult.StatusCode.ShouldBe(HttpStatusCode.Found);
        var redirectUri = signinResult.Headers.Location;
        redirectUri.ShouldNotBeNull();

        var result = await Fixture.NonRedirectingClient.GetAsync("/saml/signin_callback", CancellationToken.None);

        result.StatusCode.ShouldBe(HttpStatusCode.OK);
        var (responseXml, _, _) = await ExtractSamlResponse(result, CancellationToken.None);

        VerifySignaturePresence(responseXml, expectResponseSignature: false, expectAssertionSignature: false);

        var successResponse = await ExtractSamlSuccessFromPostAsync(result, CancellationToken.None);
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
        Fixture.ServiceProviders.Add(sp);
        await Fixture.InitializeAsync();

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
        await Fixture.NonRedirectingClient.GetAsync("/__signin", CancellationToken.None);

        var authnRequestXml = Build.AuthNRequestXml();
        var urlEncoded = await EncodeRequest(authnRequestXml, CancellationToken.None);
        var signinResult = await Fixture.NonRedirectingClient.GetAsync($"/saml/signin?SAMLRequest={urlEncoded}", CancellationToken.None);

        signinResult.StatusCode.ShouldBe(HttpStatusCode.Found);
        signinResult.Headers.Location.ShouldNotBeNull();

        var result = await Fixture.NonRedirectingClient.GetAsync("/saml/signin_callback", CancellationToken.None);

        result.StatusCode.ShouldBe(HttpStatusCode.OK);
        var (responseXml, _, _) = await ExtractSamlResponse(result, CancellationToken.None);

        VerifySignaturePresence(responseXml, expectResponseSignature: true, expectAssertionSignature: true);

        var successResponse = await ExtractSamlSuccessFromPostAsync(result, CancellationToken.None);
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
        Fixture.ServiceProviders.Add(sp);
        await Fixture.InitializeAsync();

        var claims = new List<Claim>
        {
            new Claim(JwtClaimTypes.Subject, "user-id"),
            new Claim(JwtClaimTypes.Name, "Test <User> & \"Company\""),
            new Claim("description", "Value with <tags> & \"quotes\" and 'apostrophes'"),
            new Claim("xml_data", "<root><element attr=\"value\">text</element></root>")
        };

        Fixture.UserToSignIn = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
        await Fixture.NonRedirectingClient.GetAsync("/__signin", CancellationToken.None);

        var authnRequestXml = Build.AuthNRequestXml();
        var urlEncoded = await EncodeRequest(authnRequestXml, CancellationToken.None);
        var signinResult = await Fixture.NonRedirectingClient.GetAsync($"/saml/signin?SAMLRequest={urlEncoded}", CancellationToken.None);

        signinResult.StatusCode.ShouldBe(HttpStatusCode.Found);
        signinResult.Headers.Location.ShouldNotBeNull();

        var result = await Fixture.NonRedirectingClient.GetAsync("/saml/signin_callback", CancellationToken.None);

        result.StatusCode.ShouldBe(HttpStatusCode.OK);
        var (responseXml, _, _) = await ExtractSamlResponse(result, CancellationToken.None);

        VerifySignaturePresence(responseXml, expectResponseSignature: false, expectAssertionSignature: true);

        var (_, _, responseElement) = ParseSamlResponseXml(responseXml);
        responseElement.ShouldNotBeNull();

        var successResponse = await ExtractSamlSuccessFromPostAsync(result, CancellationToken.None);
        successResponse.StatusCode.ShouldBe("urn:oasis:names:tc:SAML:2.0:status:Success");

        var nameAttribute = successResponse.Assertion.Attributes?.FirstOrDefault(a => a.Name == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name");
        nameAttribute.ShouldNotBeNull();
        nameAttribute.Value.ShouldBe("Test <User> & \"Company\"");
    }
}
