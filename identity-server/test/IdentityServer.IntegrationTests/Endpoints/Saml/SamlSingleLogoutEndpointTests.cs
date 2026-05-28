// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using System.Security.Claims;
using System.Text;
using System.Web;
using Duende.IdentityModel;
using Duende.IdentityServer.Saml;
using Duende.IdentityServer.Saml.Models;
using static Duende.IdentityServer.IntegrationTests.Endpoints.Saml.SamlTestHelpers;

namespace Duende.IdentityServer.IntegrationTests.Endpoints.Saml;

public class SamlSingleLogoutEndpointTests
{
    private const string Category = "SAML single logout endpoint";

    private const string SloPath = "/Saml2/SLO";

    private readonly Ct _ct = TestContext.Current.CancellationToken;

    private SamlFixture Fixture = new();

    private SamlData Data => Fixture.Data;

    private SamlDataBuilder Build => Fixture.Builder;

    [Fact]
    [Trait("Category", Category)]
    public async Task logout_endpoint_when_no_saml_request_in_redirect_binding_should_report_no_binding_found()
    {
        // Arrange
        Fixture.ServiceProviders.Add(Build.SamlServiceProvider());
        await Fixture.InitializeAsync();

        // Act
        var result = await Fixture.Client.GetAsync(SloPath, _ct);

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.OK);
        var errorMessage = await Fixture.GetErrorMessage(result.RequestMessage!.RequestUri, _ct);
        errorMessage.ShouldBe("No front channel binding found to satisfy request");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task logout_endpoint_when_post_binding_with_wrong_content_type_should_return_server_error()
    {
        // Arrange
        Fixture.ServiceProviders.Add(Build.SamlServiceProvider());
        await Fixture.InitializeAsync();

        var logoutRequestXml = Build.LogoutRequestXml();
        var stringContent = new StringContent(logoutRequestXml, Encoding.UTF8, "application/xml");

        // Act
        var result = await Fixture.NonRedirectingClient.PostAsync(SloPath, stringContent, _ct);

        // Assert — POST with non-form content type causes a server error when attempting to read form data
        result.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task logout_endpoint_when_post_binding_with_missing_saml_request_should_report_no_binding_found()
    {
        // Arrange
        Fixture.ServiceProviders.Add(Build.SamlServiceProvider());
        await Fixture.InitializeAsync();

        var logoutRequestXml = Build.LogoutRequestXml();
        var encodedRequest = await EncodeRequest(logoutRequestXml, _ct);
        var formData = new Dictionary<string, string>
        {
            { "wrong_form_key", encodedRequest }
        };
        var content = new FormUrlEncodedContent(formData);

        // Act
        var result = await Fixture.Client.PostAsync(SloPath, content, _ct);

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.OK);
        var errorMessage = await Fixture.GetErrorMessage(result.RequestMessage!.RequestUri, _ct);
        errorMessage.ShouldBe("No front channel binding found to satisfy request");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task logout_endpoint_when_service_provider_not_found_should_report_invalid_sp()
    {
        // Arrange
        await Fixture.InitializeAsync();

        var issuer = "https://wrong-issuer.com";
        var logoutRequestXml = Build.LogoutRequestXml(issuer: issuer);
        var urlEncoded = await EncodeRequest(logoutRequestXml, _ct);

        // Act
        var result = await Fixture.Client.GetAsync($"{SloPath}?SAMLRequest={urlEncoded}", _ct);

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.OK);
        var errorMessage = await Fixture.GetErrorMessage(result.RequestMessage!.RequestUri, _ct);
        errorMessage.ShouldBe("Invalid SP EntityId");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task logout_endpoint_when_service_provider_is_disabled_should_report_invalid_sp()
    {
        // Arrange
        var sp = Build.SamlServiceProvider();
        sp.Enabled = false;
        Fixture.ServiceProviders.Add(sp);
        await Fixture.InitializeAsync();

        var logoutRequestXml = Build.LogoutRequestXml(issuer: sp.EntityId);
        var urlEncoded = await EncodeRequest(logoutRequestXml, _ct);

        // Act
        var result = await Fixture.Client.GetAsync($"{SloPath}?SAMLRequest={urlEncoded}", _ct);

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.OK);
        var errorMessage = await Fixture.GetErrorMessage(result.RequestMessage!.RequestUri, _ct);
        errorMessage.ShouldBe("Invalid SP EntityId");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task logout_endpoint_when_service_provider_has_no_single_logout_service_url_configured_should_report_missing_slo_url()
    {
        // Arrange
        var signingCert = CreateTestSigningCertificate(Data.FakeTimeProvider);
        var sp = Build.SamlServiceProvider(signingCertificate: signingCert);
        sp.SingleLogoutServiceUrls.Clear();
        Fixture.ServiceProviders.Add(sp);
        await Fixture.InitializeAsync();

        // Sign in a user first
        Fixture.UserToSignIn =
            new ClaimsPrincipal(new ClaimsIdentity([new Claim(JwtClaimTypes.Subject, "user-id")], "Test"));
        await Fixture.Client.GetAsync("/__signin", _ct);

        var logoutRequestXml = Build.LogoutRequestXml(
            destination: new Uri($"{Fixture.Url()}{SloPath}"),
            sessionIndex: "session123");
        var urlEncoded = await EncodeAndSignRequest(logoutRequestXml, sp, _ct);

        // Act
        var result = await Fixture.Client.GetAsync($"{SloPath}?SAMLRequest={urlEncoded}", _ct);

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.OK);
        var errorMessage = await Fixture.GetErrorMessage(result.RequestMessage!.RequestUri, _ct);
        errorMessage.ShouldBe("SP does not have any SingleLogoutServiceUrls configured");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task logout_endpoint_when_saml_version_in_logout_request_is_invalid_should_report_version_mismatch()
    {
        // Arrange
        var signingCert = CreateTestSigningCertificate(Data.FakeTimeProvider);
        var sp = Build.SamlServiceProvider(signingCertificate: signingCert);
        Fixture.ServiceProviders.Add(sp);
        await Fixture.InitializeAsync();

        var logoutRequestXml = Build.LogoutRequestXml(
            destination: new Uri($"{Fixture.Url()}{SloPath}"),
            version: "1.0");
        var urlEncoded = await EncodeAndSignRequest(logoutRequestXml, sp, _ct);

        // Act
        var result = await Fixture.Client.GetAsync($"{SloPath}?SAMLRequest={urlEncoded}", _ct);

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.OK);
        var errorMessage = await Fixture.GetErrorMessage(result.RequestMessage!.RequestUri, _ct);
        errorMessage.ShouldBe("Only Version 2.0 is supported");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task logout_endpoint_when_issue_instant_is_in_future_and_no_session_should_return_success_response()
    {
        // Arrange
        var signingCert = CreateTestSigningCertificate(Data.FakeTimeProvider);
        var sp = Build.SamlServiceProvider(signingCertificate: signingCert);
        Fixture.ServiceProviders.Add(sp);
        await Fixture.InitializeAsync();

        var futureTime = Data.Now.AddMinutes(10);
        var logoutRequestXml = Build.LogoutRequestXml(
            destination: new Uri($"{Fixture.Url()}{SloPath}"),
            issueInstant: futureTime,
            sessionIndex: "session123");
        var urlEncoded = await EncodeAndSignRequest(logoutRequestXml, sp, _ct);

        // Act — use non-redirecting client so we can inspect the redirect
        var result = await Fixture.NonRedirectingClient.GetAsync($"{SloPath}?SAMLRequest={urlEncoded}", _ct);

        // Assert — IssueInstant is not validated by the new endpoint; no user session means success response
        result.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        var location = result.Headers.Location?.ToString();
        location.ShouldNotBeNullOrEmpty();
        location.ShouldContain("SAMLResponse=");
        location.ShouldStartWith(Data.SingleLogoutServiceUrl.ToString());
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task logout_endpoint_when_issue_instant_is_too_old_and_no_session_should_return_success_response()
    {
        // Arrange
        var signingCert = CreateTestSigningCertificate(Data.FakeTimeProvider);
        var sp = Build.SamlServiceProvider(signingCertificate: signingCert);
        Fixture.ServiceProviders.Add(sp);
        await Fixture.InitializeAsync();

        var oldTime = Data.Now.AddMinutes(-10);
        var logoutRequestXml = Build.LogoutRequestXml(
            destination: new Uri($"{Fixture.Url()}{SloPath}"),
            issueInstant: oldTime,
            sessionIndex: "session123");
        var urlEncoded = await EncodeAndSignRequest(logoutRequestXml, sp, _ct);

        // Act — use non-redirecting client so we can inspect the redirect
        var result = await Fixture.NonRedirectingClient.GetAsync($"{SloPath}?SAMLRequest={urlEncoded}", _ct);

        // Assert — IssueInstant is not validated by the new endpoint; no user session means success response
        result.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        var location = result.Headers.Location?.ToString();
        location.ShouldNotBeNullOrEmpty();
        location.ShouldContain("SAMLResponse=");
        location.ShouldStartWith(Data.SingleLogoutServiceUrl.ToString());
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task logout_endpoint_when_destination_does_not_match_endpoint_should_report_invalid_destination()
    {
        // Arrange
        var signingCert = CreateTestSigningCertificate(Data.FakeTimeProvider);
        var sp = Build.SamlServiceProvider(signingCertificate: signingCert);
        Fixture.ServiceProviders.Add(sp);
        await Fixture.InitializeAsync();

        var logoutRequestXml = Build.LogoutRequestXml(
            destination: new Uri("https://wrong-destination.com/saml/logout"),
            sessionIndex: "session123");
        var urlEncoded = await EncodeAndSignRequest(logoutRequestXml, sp, _ct);

        // Act
        var result = await Fixture.Client.GetAsync($"{SloPath}?SAMLRequest={urlEncoded}", _ct);

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.OK);
        var errorMessage = await Fixture.GetErrorMessage(result.RequestMessage!.RequestUri, _ct);
        errorMessage.ShouldBe("Invalid destination");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task logout_endpoint_when_service_provider_has_no_signing_certificates_configured_should_report_untrusted_signature()
    {
        // Arrange
        var sp = Build.SamlServiceProvider();
        sp.Certificates = [];
        Fixture.ServiceProviders.Add(sp);
        await Fixture.InitializeAsync();

        var logoutRequestXml = Build.LogoutRequestXml(
            destination: new Uri($"{Fixture.Url()}{SloPath}"),
            sessionIndex: "session123");
        var urlEncoded = await EncodeRequest(logoutRequestXml, _ct);

        // Act
        var result = await Fixture.Client.GetAsync($"{SloPath}?SAMLRequest={urlEncoded}", _ct);

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.OK);
        var errorMessage = await Fixture.GetErrorMessage(result.RequestMessage!.RequestUri, _ct);
        errorMessage.ShouldBe("The LogoutRequest signature is missing or not trusted");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task logout_endpoint_when_request_without_signature_received_should_report_untrusted_signature()
    {
        // Arrange
        var signingCert = CreateTestSigningCertificate(Data.FakeTimeProvider);
        var sp = Build.SamlServiceProvider(signingCertificate: signingCert);
        Fixture.ServiceProviders.Add(sp);
        await Fixture.InitializeAsync();

        // Create a logout request without a signature
        var logoutRequestXml = Build.LogoutRequestXml(
            destination: new Uri($"{Fixture.Url()}{SloPath}"),
            sessionIndex: "session123");
        var urlEncoded = await EncodeRequest(logoutRequestXml, _ct);

        // Act
        var result = await Fixture.Client.GetAsync($"{SloPath}?SAMLRequest={urlEncoded}", _ct);

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.OK);
        var errorMessage = await Fixture.GetErrorMessage(result.RequestMessage!.RequestUri, _ct);
        errorMessage.ShouldBe("The LogoutRequest signature is missing or not trusted");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task logout_endpoint_when_not_on_or_after_is_in_past_should_report_expired()
    {
        // Arrange
        var signingCert = CreateTestSigningCertificate(Data.FakeTimeProvider);
        var sp = Build.SamlServiceProvider(signingCertificate: signingCert);
        Fixture.ServiceProviders.Add(sp);
        await Fixture.InitializeAsync();

        var expiredTime = Data.Now.AddMinutes(-10);
        var logoutRequestXml = Build.LogoutRequestXml(
            destination: new Uri($"{Fixture.Url()}{SloPath}"),
            notOnOrAfter: expiredTime,
            sessionIndex: "session123");
        var urlEncoded = await EncodeAndSignRequest(logoutRequestXml, sp, _ct);

        // Act
        var result = await Fixture.Client.GetAsync($"{SloPath}?SAMLRequest={urlEncoded}", _ct);

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.OK);
        var errorMessage = await Fixture.GetErrorMessage(result.RequestMessage!.RequestUri, _ct);
        errorMessage.ShouldBe("LogoutRequest has expired (NotOnOrAfter)");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task logout_endpoint_when_no_authenticated_session_should_return_success_response()
    {
        // Arrange
        var signingCert = CreateTestSigningCertificate(Data.FakeTimeProvider);
        var sp = Build.SamlServiceProvider(signingCertificate: signingCert);
        Fixture.ServiceProviders.Add(sp);
        await Fixture.InitializeAsync();

        // Don't sign in a user - no authenticated session

        var logoutRequestXml = Build.LogoutRequestXml(
            destination: new Uri($"{Fixture.Url()}{SloPath}"),
            sessionIndex: "session123");
        var urlEncoded = await EncodeAndSignRequest(logoutRequestXml, sp, _ct);

        // Act — use non-redirecting client so we can inspect the redirect
        var result = await Fixture.NonRedirectingClient.GetAsync($"{SloPath}?SAMLRequest={urlEncoded}", _ct);

        // Assert — SP uses HTTP-Redirect binding, so response is a redirect to SP's SLO URL with SAMLResponse
        result.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        var location = result.Headers.Location?.ToString();
        location.ShouldNotBeNullOrEmpty();
        location.ShouldContain("SAMLResponse=");
        location.ShouldStartWith(Data.SingleLogoutServiceUrl.ToString());
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task logout_endpoint_when_no_session_found_for_session_index_should_return_success_response()
    {
        // Arrange
        var signingCert = CreateTestSigningCertificate(Data.FakeTimeProvider);
        var sp = Build.SamlServiceProvider(signingCertificate: signingCert);
        Fixture.ServiceProviders.Add(sp);
        var anotherSp = Build.SamlServiceProvider(signingCertificate: signingCert);
        sp.EntityId = "https://another-sp.com";
        Fixture.ServiceProviders.Add(anotherSp);
        await Fixture.InitializeAsync();

        // Sign in a user first
        Fixture.UserToSignIn =
            new ClaimsPrincipal(new ClaimsIdentity([new Claim(JwtClaimTypes.Subject, "user-id")], "Test"));
        await Fixture.Client.GetAsync("/__signin", _ct);

        // Use a different service provider than what was established
        var logoutRequestXml = Build.LogoutRequestXml(
            issuer: anotherSp.EntityId, // Use a different SP so session will not be found
            destination: new Uri($"{Fixture.Url()}{SloPath}"));
        var urlEncoded = await EncodeAndSignRequest(logoutRequestXml, sp, _ct);

        // Act — use non-redirecting client so we can inspect the redirect
        var result = await Fixture.NonRedirectingClient.GetAsync($"{SloPath}?SAMLRequest={urlEncoded}", _ct);

        // Assert — SP uses HTTP-Redirect binding, so response is a redirect to SP's SLO URL with SAMLResponse
        result.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        var location = result.Headers.Location?.ToString();
        location.ShouldNotBeNullOrEmpty();
        location.ShouldContain("SAMLResponse=");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task logout_endpoint_when_wrong_session_index_sent_should_return_success()
    {
        // Arrange
        var signingCert = CreateTestSigningCertificate(Data.FakeTimeProvider);
        var sp = Build.SamlServiceProvider(signingCertificate: signingCert);
        Fixture.ServiceProviders.Add(sp);
        await Fixture.InitializeAsync();

        // Sign in a user and perform SSO to establish a real SAML session
        Fixture.UserToSignIn =
            new ClaimsPrincipal(new ClaimsIdentity([new Claim(JwtClaimTypes.Subject, "user-id")], "Test"));
        await Fixture.Client.GetAsync("/__signin", _ct);

        var (_, nameId) = await PerformSigninAndExtractSessionInfo(Fixture, sp, _ct);

        // Use a different session index than what was established
        var logoutRequestXml = Build.LogoutRequestXml(
            destination: new Uri($"{Fixture.Url()}{SloPath}"),
            sessionIndex: "wrong-session-index",
            nameId: nameId);
        var urlEncoded = await EncodeAndSignRequest(logoutRequestXml, sp, _ct);

        // Act — disable auto-redirect to capture the SAML response redirect
        Fixture.Client.AllowAutoRedirect = false;
        var result = await Fixture.Client.GetAsync($"{SloPath}?SAMLRequest={urlEncoded}", _ct);
        Fixture.Client.AllowAutoRedirect = true;

        // Assert — should get a SAML LogoutResponse with Success status via redirect binding
        var samlResponse = ExtractSamlLogoutResponseFromRedirect(result);
        samlResponse.StatusCode.ShouldBe(SamlStatusCodes.Success);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task logout_endpoint_when_request_is_valid_should_redirect_to_logout()
    {
        // Arrange
        var signingCert = CreateTestSigningCertificate(Data.FakeTimeProvider);
        var sp = Build.SamlServiceProvider(signingCertificate: signingCert);
        Fixture.ServiceProviders.Add(sp);
        await Fixture.InitializeAsync();

        // Sign in a user and perform SSO to establish a real SAML session
        Fixture.UserToSignIn =
            new ClaimsPrincipal(new ClaimsIdentity([new Claim(JwtClaimTypes.Subject, "user-id")], "Test"));
        await Fixture.Client.GetAsync("/__signin", _ct);

        var (sessionIndex, nameId) = await PerformSigninAndExtractSessionInfo(Fixture, sp, _ct);

        var logoutRequestXml = Build.LogoutRequestXml(
            destination: new Uri($"{Fixture.Url()}{SloPath}"),
            sessionIndex: sessionIndex,
            nameId: nameId);
        var urlEncoded = await EncodeAndSignRequest(logoutRequestXml, sp, _ct);

        // Act
        var result = await Fixture.Client.GetAsync($"{SloPath}?SAMLRequest={urlEncoded}", _ct);

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.OK);
        var requestUrl = result.RequestMessage?.RequestUri;
        requestUrl.ShouldNotBeNull();
        requestUrl.AbsolutePath.ShouldBe(Fixture.LogoutUrl.ToString());
        var queryStringValues = HttpUtility.ParseQueryString(requestUrl.Query);
        queryStringValues["logoutId"].ShouldNotBeNullOrWhiteSpace();
    }

    [Fact(Skip = "Endpoint is no longer responsible for logout as Host app logout page is now responsible. Return to this after propagated logout is complete and adjust/remove as appropriate")]
    [Trait("Category", Category)]
    public async Task logout_endpoint_when_logout_is_successful_should_terminate_user_session()
    {
        // Arrange
        var signingCert = CreateTestSigningCertificate(Data.FakeTimeProvider);
        var sp = Build.SamlServiceProvider(signingCertificate: signingCert);
        Fixture.ServiceProviders.Add(sp);
        await Fixture.InitializeAsync();

        // Sign in a user first
        Fixture.UserToSignIn =
            new ClaimsPrincipal(new ClaimsIdentity([new Claim(JwtClaimTypes.Subject, "user-id")], "Test"));
        await Fixture.Client.GetAsync("/__signin", _ct);

        // Ensure user can access protected resource
        var initialProtectedResourceResult = await Fixture.Client.GetAsync("__protected-resource", _ct);
        initialProtectedResourceResult.StatusCode.ShouldBe(HttpStatusCode.OK);

        var (sessionIndex, nameId) = await PerformSigninAndExtractSessionInfo(Fixture, sp, _ct);

        var logoutRequestXml = Build.LogoutRequestXml(
            destination: new Uri($"{Fixture.Url()}{SloPath}"),
            sessionIndex: sessionIndex,
            nameId: nameId);
        var urlEncoded = await EncodeAndSignRequest(logoutRequestXml, sp, _ct);

        // Act
        var result = await Fixture.Client.GetAsync($"{SloPath}?SAMLRequest={urlEncoded}", _ct);

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.OK); // Follows redirect

        // Verify user can no longer access protected resource and is redirected to login
        var finalProtectedResourceResult = await Fixture.Client.GetAsync("__protected-resource", _ct);
        finalProtectedResourceResult.StatusCode.ShouldBe(HttpStatusCode.OK);
        finalProtectedResourceResult.RequestMessage?.RequestUri?.AbsoluteUri.ShouldStartWith($"{Fixture.Url()}{Fixture.LoginUrl.ToString()}");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task logout_endpoint_when_redirect_binding_with_invalid_base64_should_report_invalid_encoding()
    {
        // Arrange
        Fixture.ServiceProviders.Add(Build.SamlServiceProvider());
        await Fixture.InitializeAsync();

        var invalidBase64 = "not-valid-base64!!!";

        // Act
        var result = await Fixture.Client.GetAsync($"{SloPath}?SAMLRequest={invalidBase64}", _ct);

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.OK);
        var errorMessage = await Fixture.GetErrorMessage(result.RequestMessage!.RequestUri, _ct);
        errorMessage.ShouldBe("Invalid base64 encoding in SAML logout message");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task logout_endpoint_when_form_post_with_invalid_base64_should_report_invalid_encoding()
    {
        // Arrange
        Fixture.ServiceProviders.Add(Build.SamlServiceProvider());
        await Fixture.InitializeAsync();

        var invalidBase64 = "not-valid-base64!!!";
        var formData = new Dictionary<string, string>
        {
            { "SAMLRequest", invalidBase64 }
        };
        var content = new FormUrlEncodedContent(formData);

        // Act
        var result = await Fixture.Client.PostAsync(SloPath, content, _ct);

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.OK);
        var errorMessage = await Fixture.GetErrorMessage(result.RequestMessage!.RequestUri, _ct);
        errorMessage.ShouldBe("Invalid base64 encoding in SAML logout message");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task logout_endpoint_when_request_has_no_id_attribute_should_report_parsing_error()
    {
        // Arrange
        Fixture.ServiceProviders.Add(Build.SamlServiceProvider());
        await Fixture.InitializeAsync();

        var logoutRequestXml = Build.LogoutRequestXml();
        var xmlWithoutId = logoutRequestXml.Replace($"ID=\"{Data.RequestId}\"", "");

        var urlEncoded = await EncodeRequest(xmlWithoutId, _ct);

        // Act
        var result = await Fixture.Client.GetAsync($"{SloPath}?SAMLRequest={urlEncoded}", _ct);

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.OK);
        var errorMessage = await Fixture.GetErrorMessage(result.RequestMessage!.RequestUri, _ct);
        errorMessage.ShouldBe("The SAML logout request could not be processed");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task logout_endpoint_when_request_has_empty_id_should_report_untrusted_signature()
    {
        // Arrange — empty ID still parses, but unsigned request fails signature check
        var signingCert = CreateTestSigningCertificate(Data.FakeTimeProvider);
        var sp = Build.SamlServiceProvider(signingCertificate: signingCert);
        Fixture.ServiceProviders.Add(sp);
        await Fixture.InitializeAsync();

        var logoutRequestXml = Build.LogoutRequestXml(requestId: "");
        var urlEncoded = await EncodeRequest(logoutRequestXml, _ct);

        // Act
        var result = await Fixture.Client.GetAsync($"{SloPath}?SAMLRequest={urlEncoded}", _ct);

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.OK);
        var errorMessage = await Fixture.GetErrorMessage(result.RequestMessage!.RequestUri, _ct);
        errorMessage.ShouldBe("The LogoutRequest signature is missing or not trusted");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task logout_endpoint_when_request_issuer_is_empty_should_report_missing_sp()
    {
        // Arrange
        Fixture.ServiceProviders.Add(Build.SamlServiceProvider());
        await Fixture.InitializeAsync();

        var logoutRequestXml = Build.LogoutRequestXml(issuer: "");
        var urlEncoded = await EncodeRequest(logoutRequestXml, _ct);

        // Act
        var result = await Fixture.Client.GetAsync($"{SloPath}?SAMLRequest={urlEncoded}", _ct);

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.OK);
        var errorMessage = await Fixture.GetErrorMessage(result.RequestMessage!.RequestUri, _ct);
        errorMessage.ShouldBe("Invalid SP EntityId");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task logout_endpoint_when_request_issuer_is_missing_should_report_missing_sp()
    {
        // Arrange
        Fixture.ServiceProviders.Add(Build.SamlServiceProvider());
        await Fixture.InitializeAsync();

        var logoutRequestXml = Build.LogoutRequestXml();
        var xmlWithoutIssuer = logoutRequestXml.Replace(
            $"<saml:Issuer>{Data.EntityId}</saml:Issuer>",
            ""
        );

        var urlEncoded = await EncodeRequest(xmlWithoutIssuer, _ct);

        // Act
        var result = await Fixture.Client.GetAsync($"{SloPath}?SAMLRequest={urlEncoded}", _ct);

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.OK);
        var errorMessage = await Fixture.GetErrorMessage(result.RequestMessage!.RequestUri, _ct);
        errorMessage.ShouldBe("Missing SP EntityID in LogoutRequest");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task logout_callback_endpoint_when_no_logout_id_should_report_missing_state()
    {
        // Arrange
        Fixture.ServiceProviders.Add(Build.SamlServiceProvider());
        await Fixture.InitializeAsync();

        // Act
        var result = await Fixture.Client.GetAsync("/Saml2/SLO/Callback", _ct);

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.OK);
        var errorMessage = await Fixture.GetErrorMessage(result.RequestMessage!.RequestUri, _ct);
        errorMessage.ShouldBe("Missing or invalid SAML logout state identifier");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task logout_response_records_success_in_session_store()
    {
        // Arrange
        var sp = Build.SamlServiceProvider();
        sp.RequireSignedLogoutResponses = false;
        Fixture.ServiceProviders.Add(sp);
        await Fixture.InitializeAsync();

        // Pre-populate the session store with an expected response
        var sessionStore = Fixture.Get<ISamlLogoutSessionStore>();
        var session = new SamlLogoutSession
        {
            LogoutId = "test-logout",
            ExpectedResponses = new Dictionary<string, ExpectedSpLogout>
            {
                ["_req-123"] = new(sp.EntityId)
            },
            CreatedUtc = Data.Now,
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(5)
        };
        await sessionStore.StoreAsync(session, _ct);

        // Build a LogoutResponse from the SP
        var logoutResponseXml = Build.LogoutResponseXml(
            inResponseTo: "_req-123",
            destination: new Uri($"{Fixture.Url()}{SloPath}"));
        var encoded = await EncodeRequest(logoutResponseXml, _ct);

        // Act — send the LogoutResponse to the SLO endpoint
        var result = await Fixture.NonRedirectingClient.GetAsync(
            $"{SloPath}?SAMLResponse={encoded}", _ct);

        // Assert — endpoint acknowledges with 200
        result.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Verify the response was recorded in the store
        var updated = await sessionStore.GetByLogoutIdAsync("test-logout", _ct);
        updated.ShouldNotBeNull();
        updated.ExpectedResponses["_req-123"].Response.ShouldNotBeNull();
        updated.ExpectedResponses["_req-123"].Response!.Success.ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task logout_response_records_failure_in_session_store()
    {
        // Arrange
        var sp = Build.SamlServiceProvider();
        sp.RequireSignedLogoutResponses = false;
        Fixture.ServiceProviders.Add(sp);
        await Fixture.InitializeAsync();

        var sessionStore = Fixture.Get<ISamlLogoutSessionStore>();
        var session = new SamlLogoutSession
        {
            LogoutId = "test-logout",
            ExpectedResponses = new Dictionary<string, ExpectedSpLogout>
            {
                ["_req-456"] = new(sp.EntityId)
            },
            CreatedUtc = Data.Now,
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(5)
        };
        await sessionStore.StoreAsync(session, _ct);

        // Build a LogoutResponse with a non-success status
        var logoutResponseXml = Build.LogoutResponseXml(
            inResponseTo: "_req-456",
            statusCode: "urn:oasis:names:tc:SAML:2.0:status:Responder",
            destination: new Uri($"{Fixture.Url()}{SloPath}"));
        var encoded = await EncodeRequest(logoutResponseXml, _ct);

        // Act
        var result = await Fixture.NonRedirectingClient.GetAsync(
            $"{SloPath}?SAMLResponse={encoded}", _ct);

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.OK);

        var updated = await sessionStore.GetByLogoutIdAsync("test-logout", _ct);
        updated.ShouldNotBeNull();
        updated.ExpectedResponses["_req-456"].Response.ShouldNotBeNull();
        updated.ExpectedResponses["_req-456"].Response!.Success.ShouldBeFalse();
    }

}
