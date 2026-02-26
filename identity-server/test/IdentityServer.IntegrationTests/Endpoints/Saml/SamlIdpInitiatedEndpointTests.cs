// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Web;
using Duende.IdentityModel;
using Duende.IdentityServer.Internal.Saml;
using Microsoft.AspNetCore.Mvc;
using static Duende.IdentityServer.IntegrationTests.Endpoints.Saml.SamlTestHelpers;

namespace Duende.IdentityServer.IntegrationTests.Endpoints.Saml;

public class SamlIdpInitiatedEndpointTests
{
    private const string Category = "SAML IdP-Initiated Endpoint";

    private readonly Ct _ct = TestContext.Current.CancellationToken;

    private SamlFixture Fixture = new();

    private SamlData Data => Fixture.Data;

    private SamlDataBuilder Build => Fixture.Builder;

    [Fact]
    [Trait("Category", Category)]
    public async Task can_initiate_idp_sso_when_user_is_authenticated()
    {
        // Arrange
        var sp = Build.SamlServiceProvider();
        sp.AllowIdpInitiated = true;
        Fixture.ServiceProviders.Add(sp);
        Fixture.ConfigureIdentityServerOptions = options =>
        {
            options.Endpoints.EnableSamlIdpInitiatedEndpoint = true;
        };
        await Fixture.InitializeAsync();

        Fixture.UserToSignIn =
            new ClaimsPrincipal(new ClaimsIdentity([new Claim(JwtClaimTypes.Subject, "user-id")], "Test"));
        await Fixture.NonRedirectingClient.GetAsync("/__signin", _ct);

        // Act
        var spEntityId = HttpUtility.UrlEncode(Data.EntityId.ToString());
        var result = await Fixture.NonRedirectingClient.GetAsync($"/saml/idp-initiated?spEntityId={spEntityId}", _ct);

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.Found);
        var redirectUri = result.Headers.Location;
        redirectUri.ShouldNotBeNull();
        redirectUri.ToString().ShouldBe($"{SamlConstants.Urls.SamlRoute}{SamlConstants.Urls.SigninCallback}");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task can_initiate_idp_sso_with_relay_state()
    {
        // Arrange
        var sp = Build.SamlServiceProvider();
        sp.AllowIdpInitiated = true;
        Fixture.ServiceProviders.Add(sp);
        Fixture.ConfigureIdentityServerOptions = options =>
        {
            options.Endpoints.EnableSamlIdpInitiatedEndpoint = true;
        };
        await Fixture.InitializeAsync();

        Fixture.UserToSignIn =
            new ClaimsPrincipal(new ClaimsIdentity([new Claim(JwtClaimTypes.Subject, "user-id")], "Test"));
        await Fixture.NonRedirectingClient.GetAsync("/__signin", _ct);

        // Act
        var spEntityId = HttpUtility.UrlEncode(Data.EntityId.ToString());
        var relayState = HttpUtility.UrlEncode("/my-app/dashboard");
        var result = await Fixture.NonRedirectingClient.GetAsync(
            $"/saml/idp-initiated?spEntityId={spEntityId}&relayState={relayState}", _ct);

        result.StatusCode.ShouldBe(HttpStatusCode.Found);
        var redirectUri = result.Headers.Location;
        redirectUri.ShouldNotBeNull();
        redirectUri.ToString().ShouldBe($"{SamlConstants.Urls.SamlRoute}{SamlConstants.Urls.SigninCallback}");

        var acsResult = await Fixture.NonRedirectingClient.GetAsync(redirectUri, _ct);

        // Assert
        var samlResponse = await ExtractSamlSuccessFromPostAsync(acsResult, _ct);
        samlResponse.RelayState.ShouldBe(HttpUtility.UrlDecode(relayState));
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task redirects_to_login_when_user_not_authenticated()
    {
        // Arrange
        var sp = Build.SamlServiceProvider();
        sp.AllowIdpInitiated = true;
        Fixture.ServiceProviders.Add(sp);
        Fixture.ConfigureIdentityServerOptions = options =>
        {
            options.Endpoints.EnableSamlIdpInitiatedEndpoint = true;
        };
        await Fixture.InitializeAsync();

        // Act
        var spEntityId = HttpUtility.UrlEncode(Data.EntityId.ToString());
        var result = await Fixture.NonRedirectingClient.GetAsync($"/saml/idp-initiated?spEntityId={spEntityId}", _ct);

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.Found);
        var redirectUri = result.Headers.Location;
        redirectUri.ShouldNotBeNull();
        HttpUtility.UrlDecode(redirectUri.ToString()).ShouldBe($"{Fixture.LoginUrl}?ReturnUrl={Fixture.SignInCallbackUrl}");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task returns_error_when_sp_not_registered()
    {
        // Arrange
        Fixture.ServiceProviders.Add(Build.SamlServiceProvider());
        Fixture.ConfigureIdentityServerOptions = options =>
        {
            options.Endpoints.EnableSamlIdpInitiatedEndpoint = true;
        };
        await Fixture.InitializeAsync();

        // Act
        var unknownEntityId = HttpUtility.UrlEncode("https://unknown.example.com");
        var result = await Fixture.Client.GetAsync($"/saml/idp-initiated?spEntityId={unknownEntityId}", _ct);

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problemDetails = await result.Content.ReadFromJsonAsync<ProblemDetails>(_ct);
        problemDetails.ShouldNotBeNull();
        problemDetails.Detail.ShouldBe("Service Provider 'https://unknown.example.com' is not registered");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task returns_error_when_sp_is_disabled()
    {
        // Arrange
        // Note: The InMemoryServiceProviderStore filters disabled SPs,
        // so they appear as "not registered". This is correct behavior.
        var sp = Build.SamlServiceProvider();
        sp.Enabled = false;
        sp.AllowIdpInitiated = true;
        Fixture.ServiceProviders.Add(sp);
        Fixture.ConfigureIdentityServerOptions = options =>
        {
            options.Endpoints.EnableSamlIdpInitiatedEndpoint = true;
        };
        await Fixture.InitializeAsync();

        // Act
        var spEntityId = HttpUtility.UrlEncode(Data.EntityId.ToString());
        var result = await Fixture.Client.GetAsync($"/saml/idp-initiated?spEntityId={spEntityId}", _ct);

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problemDetails = await result.Content.ReadFromJsonAsync<ProblemDetails>(_ct);
        problemDetails.ShouldNotBeNull();
        // Disabled SPs are filtered by the store, so they appear as not registered
        problemDetails.Detail.ShouldBe($"Service Provider '{Data.EntityId}' is not registered");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task returns_error_when_idp_initiated_not_allowed()
    {
        // Arrange
        var sp = Build.SamlServiceProvider();
        sp.AllowIdpInitiated = false;
        Fixture.ServiceProviders.Add(sp);
        Fixture.ConfigureIdentityServerOptions = options =>
        {
            options.Endpoints.EnableSamlIdpInitiatedEndpoint = true;
        };
        await Fixture.InitializeAsync();

        // Act
        var spEntityId = HttpUtility.UrlEncode(Data.EntityId.ToString());
        var result = await Fixture.Client.GetAsync($"/saml/idp-initiated?spEntityId={spEntityId}", _ct);

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problemDetails = await result.Content.ReadFromJsonAsync<ProblemDetails>(_ct);
        problemDetails.ShouldNotBeNull();
        problemDetails.Detail.ShouldBe($"Service Provider '{Data.EntityId}' does not allow IdP-initiated SSO");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task returns_error_when_relay_state_exceeds_max_length()
    {
        // Arrange
        Fixture.ConfigureSamlOptions = options =>
        {
            options.MaxRelayStateLength = 50;
        };

        var sp = Build.SamlServiceProvider();
        sp.AllowIdpInitiated = true;
        Fixture.ServiceProviders.Add(sp);
        Fixture.ConfigureIdentityServerOptions = options =>
        {
            options.Endpoints.EnableSamlIdpInitiatedEndpoint = true;
        };
        await Fixture.InitializeAsync();

        // Act
        var spEntityId = HttpUtility.UrlEncode(Data.EntityId.ToString());
        var longRelayState = HttpUtility.UrlEncode(new string('a', 100));
        var result = await Fixture.Client.GetAsync(
            $"/saml/idp-initiated?spEntityId={spEntityId}&relayState={longRelayState}", _ct);

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problemDetails = await result.Content.ReadFromJsonAsync<ProblemDetails>(_ct);
        problemDetails.ShouldNotBeNull();
        problemDetails.Detail.ShouldBe("RelayState exceeds maximum length of 50 bytes");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task returns_error_when_sp_has_no_acs_urls()
    {
        // Arrange
        var sp = Build.SamlServiceProvider();
        sp.AllowIdpInitiated = true;
        sp.AssertionConsumerServiceUrls = Array.Empty<Uri>();
        Fixture.ServiceProviders.Add(sp);
        Fixture.ConfigureIdentityServerOptions = options =>
        {
            options.Endpoints.EnableSamlIdpInitiatedEndpoint = true;
        };
        await Fixture.InitializeAsync();

        // Act
        var spEntityId = HttpUtility.UrlEncode(Data.EntityId.ToString());
        var result = await Fixture.Client.GetAsync($"/saml/idp-initiated?spEntityId={spEntityId}", _ct);

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problemDetails = await result.Content.ReadFromJsonAsync<ProblemDetails>(_ct);
        problemDetails.ShouldNotBeNull();
        problemDetails.Detail.ShouldBe($"Service Provider '{Data.EntityId}' has no AssertionConsumerServiceUrls configured");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task uses_first_acs_url_when_multiple_configured()
    {
        // Arrange
        var firstAcsUrl = new Uri("https://sp.example.com/acs/first");
        var secondAcsUrl = new Uri("https://sp.example.com/acs/second");

        var sp = Build.SamlServiceProvider();
        sp.AllowIdpInitiated = true;
        sp.AssertionConsumerServiceUrls = [firstAcsUrl, secondAcsUrl];
        Fixture.ServiceProviders.Add(sp);
        Fixture.ConfigureIdentityServerOptions = options =>
        {
            options.Endpoints.EnableSamlIdpInitiatedEndpoint = true;
        };
        await Fixture.InitializeAsync();

        Fixture.UserToSignIn =
            new ClaimsPrincipal(new ClaimsIdentity([new Claim(JwtClaimTypes.Subject, "user-id")], "Test"));
        await Fixture.NonRedirectingClient.GetAsync("/__signin", _ct);

        // Act
        var spEntityId = HttpUtility.UrlEncode(Data.EntityId.ToString());
        var result = await Fixture.NonRedirectingClient.GetAsync($"/saml/idp-initiated?spEntityId={spEntityId}", _ct);

        result.StatusCode.ShouldBe(HttpStatusCode.Found);
        var redirectUri = result.Headers.Location;
        redirectUri.ShouldNotBeNull();
        redirectUri.ToString().ShouldBe($"{SamlConstants.Urls.SamlRoute}{SamlConstants.Urls.SigninCallback}");

        var acsResult = await Fixture.NonRedirectingClient.GetAsync(redirectUri.ToString(), _ct);

        // Assert
        var samlResponse = await ExtractSamlSuccessFromPostAsync(acsResult, _ct);
        samlResponse.Destination.ShouldBe(firstAcsUrl.ToString());
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task can_complete_full_idp_initiated_flow()
    {
        // Arrange
        var sp = Build.SamlServiceProvider();
        sp.AllowIdpInitiated = true;
        Fixture.ServiceProviders.Add(sp);
        Fixture.ConfigureIdentityServerOptions = options =>
        {
            options.Endpoints.EnableSamlIdpInitiatedEndpoint = true;
        };
        await Fixture.InitializeAsync();

        Fixture.UserToSignIn =
            new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(JwtClaimTypes.Subject, "user-123"),
                    new Claim(JwtClaimTypes.Email, "user@example.com"),
                    new Claim(JwtClaimTypes.Name, "Test User")
                ], "Test"));
        await Fixture.NonRedirectingClient.GetAsync("/__signin", _ct);

        var spEntityId = HttpUtility.UrlEncode(Data.EntityId.ToString());
        var relayState = HttpUtility.UrlEncode("/target/page");

        // Act
        // Step 1: Initiate IdP SSO
        var initiateResult = await Fixture.NonRedirectingClient.GetAsync(
            $"/saml/idp-initiated?spEntityId={spEntityId}&relayState={relayState}", _ct);

        initiateResult.StatusCode.ShouldBe(HttpStatusCode.Found);
        initiateResult.Headers.Location.ShouldNotBeNull();
        initiateResult.Headers.Location.ToString().ShouldBe("/saml/signin_callback");

        var stateId = ExtractStateIdFromCookie(initiateResult);
        stateId.ShouldNotBeNull();

        // Step 2: Follow redirect to signin callback
        var callbackResult = await Fixture.NonRedirectingClient.GetAsync("/saml/signin_callback", _ct);

        // Assert
        callbackResult.StatusCode.ShouldBe(HttpStatusCode.OK);

        var samlResponse = await ExtractSamlSuccessFromPostAsync(callbackResult, _ct);

        samlResponse.ShouldNotBeNull();
        samlResponse.Issuer.ShouldBe(Fixture.Url());
        samlResponse.Destination.ShouldBe(Data.AcsUrl.ToString());
        samlResponse.StatusCode.ShouldBe("urn:oasis:names:tc:SAML:2.0:status:Success");

        samlResponse.InResponseTo.ShouldBeNull();

        samlResponse.RelayState.ShouldBe("/target/page");

        samlResponse.Assertion.Subject.ShouldNotBeNull();
        samlResponse.Assertion.Subject.NameId.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task endpoint_disabled_when_configuration_disables_it()
    {
        // Arrange
        Fixture.ConfigureIdentityServerOptions = options =>
        {
            options.Endpoints.EnableSamlIdpInitiatedEndpoint = false;
        };

        var sp = Build.SamlServiceProvider();
        sp.AllowIdpInitiated = true;
        Fixture.ServiceProviders.Add(sp);
        await Fixture.InitializeAsync();

        // Act
        var spEntityId = HttpUtility.UrlEncode(Data.EntityId.ToString());
        var result = await Fixture.Client.GetAsync($"/saml/idp-initiated?spEntityId={spEntityId}", _ct);

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task missing_sp_entity_id_parameter_returns_bad_request()
    {
        Fixture.ServiceProviders.Add(Build.SamlServiceProvider());
        Fixture.ConfigureIdentityServerOptions = options =>
        {
            options.Endpoints.EnableSamlIdpInitiatedEndpoint = true;
        };
        await Fixture.InitializeAsync();

        var result = await Fixture.Client.GetAsync("/saml/idp-initiated", _ct);

        result.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }
}
