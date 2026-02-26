// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Web;
using Duende.IdentityModel;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Saml.Models;
using Microsoft.AspNetCore.Mvc;
using static Duende.IdentityServer.IntegrationTests.Endpoints.Saml.SamlTestHelpers;

namespace Duende.IdentityServer.IntegrationTests.Endpoints.Saml;

public class SamlSingleLogoutEndpointTests
{
    private const string Category = "SAML single logout endpoint";

    private readonly Ct _ct = TestContext.Current.CancellationToken;

    private SamlFixture Fixture = new();

    private SamlData Data => Fixture.Data;

    private SamlDataBuilder Build => Fixture.Builder;

    [Fact]
    [Trait("Category", Category)]
    public async Task logout_endpoint_when_no_saml_request_in_redirect_binding_should_return_bad_request()
    {
        // Arrange
        Fixture.ServiceProviders.Add(Build.SamlServiceProvider());
        await Fixture.InitializeAsync();

        // Act
        var result = await Fixture.Client.GetAsync("/saml/logout", _ct);

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problemDetails = await result.Content.ReadFromJsonAsync<ProblemDetails>(_ct);
        problemDetails.ShouldNotBeNull();
        problemDetails.Detail.ShouldBe("Missing 'SAMLRequest' query parameter in SAML logout request");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task logout_endpoint_when_post_binding_with_wrong_content_type_should_return_bad_request()
    {
        // Arrange
        Fixture.ServiceProviders.Add(Build.SamlServiceProvider());
        await Fixture.InitializeAsync();

        var logoutRequestXml = Build.LogoutRequestXml();
        var stringContent = new StringContent(logoutRequestXml, Encoding.UTF8, "application/xml");

        // Act
        var result = await Fixture.Client.PostAsync("/saml/logout", stringContent, _ct);

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problemDetails = await result.Content.ReadFromJsonAsync<ProblemDetails>(_ct);
        problemDetails.ShouldNotBeNull();
        problemDetails.Detail.ShouldBe("POST request does not have form content type for SAML logout request");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task logout_endpoint_when_post_binding_with_missing_saml_request_should_return_bad_request()
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
        var result = await Fixture.Client.PostAsync("/saml/logout", content, _ct);

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problemDetails = await result.Content.ReadFromJsonAsync<ProblemDetails>(_ct);
        problemDetails.ShouldNotBeNull();
        problemDetails.Detail.ShouldBe("Missing 'SAMLRequest' form parameter in SAML logout request");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task logout_endpoint_when_service_provider_not_found_should_return_bad_request()
    {
        // Arrange
        await Fixture.InitializeAsync();

        var issuer = "https://wrong-issuer.com";
        var logoutRequestXml = Build.LogoutRequestXml(issuer: issuer);
        var urlEncoded = await EncodeRequest(logoutRequestXml, _ct);

        // Act
        var result = await Fixture.Client.GetAsync($"/saml/logout?SAMLRequest={urlEncoded}", _ct);

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problemDetails = await result.Content.ReadFromJsonAsync<ProblemDetails>(_ct);
        problemDetails.ShouldNotBeNull();
        problemDetails.Detail.ShouldBe($"Service Provider '{issuer}' is not registered or is disabled");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task logout_endpoint_when_service_provider_is_disabled_should_return_bad_request()
    {
        // Arrange
        var sp = Build.SamlServiceProvider();
        sp.Enabled = false;
        Fixture.ServiceProviders.Add(sp);
        await Fixture.InitializeAsync();

        var logoutRequestXml = Build.LogoutRequestXml(issuer: sp.EntityId);
        var urlEncoded = await EncodeRequest(logoutRequestXml, _ct);

        // Act
        var result = await Fixture.Client.GetAsync($"/saml/logout?SAMLRequest={urlEncoded}", _ct);

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problemDetails = await result.Content.ReadFromJsonAsync<ProblemDetails>(_ct);
        problemDetails.ShouldNotBeNull();
        problemDetails.Detail.ShouldBe($"Service Provider '{sp.EntityId}' is not registered or is disabled");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task logout_endpoint_when_service_provider_has_no_single_logout_service_url_configured_should_return_error()
    {
        // Arrange
        var signingCert = CreateTestSigningCertificate(Data.FakeTimeProvider);
        var sp = Build.SamlServiceProvider(signingCertificate: signingCert);
        sp.SingleLogoutServiceUrl = null;
        Fixture.ServiceProviders.Add(sp);
        await Fixture.InitializeAsync();

        // Sign in a user first
        Fixture.UserToSignIn =
            new ClaimsPrincipal(new ClaimsIdentity([new Claim(JwtClaimTypes.Subject, "user-id")], "Test"));
        await Fixture.Client.GetAsync("/__signin", _ct);

        var logoutRequestXml = Build.LogoutRequestXml(
            destination: new Uri($"{Fixture.Url()}/saml/logout"),
            sessionIndex: "session123");
        var urlEncoded = await EncodeAndSignRequest(logoutRequestXml, sp, _ct);

        // Act
        var result = await Fixture.Client.GetAsync($"/saml/logout?SAMLRequest={urlEncoded}", _ct);

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problemDetails = await result.Content.ReadFromJsonAsync<ProblemDetails>(_ct);
        problemDetails.ShouldNotBeNull();
        problemDetails.Detail.ShouldBe($"Service Provider '{sp.EntityId}' has no SingleLogoutServiceUrl configured");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task logout_endpoint_when_saml_version_in_logout_request_is_invalid_should_return_error_response()
    {
        // Arrange
        var signingCert = CreateTestSigningCertificate(Data.FakeTimeProvider);
        var sp = Build.SamlServiceProvider(signingCertificate: signingCert);
        Fixture.ServiceProviders.Add(sp);
        await Fixture.InitializeAsync();

        var logoutRequestXml = Build.LogoutRequestXml(
            destination: new Uri($"{Fixture.Url()}/saml/logout"),
            version: "1.0");
        var urlEncoded = await EncodeAndSignRequest(logoutRequestXml, sp, _ct);

        // Act
        var result = await Fixture.Client.GetAsync($"/saml/logout?SAMLRequest={urlEncoded}", _ct);

        // Assert
        var logoutResponse = await ExtractSamlLogoutResponseFromPostAsync(result, _ct);
        logoutResponse.StatusCode.ShouldBe(SamlStatusCodes.VersionMismatch);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task logout_endpoint_when_issue_instant_is_in_future_should_return_error_response()
    {
        // Arrange
        var signingCert = CreateTestSigningCertificate(Data.FakeTimeProvider);
        var sp = Build.SamlServiceProvider(signingCertificate: signingCert);
        Fixture.ServiceProviders.Add(sp);
        await Fixture.InitializeAsync();

        var futureTime = Data.Now.AddMinutes(10);
        var logoutRequestXml = Build.LogoutRequestXml(
            destination: new Uri($"{Fixture.Url()}/saml/logout"),
            issueInstant: futureTime,
            sessionIndex: "session123");
        var urlEncoded = await EncodeAndSignRequest(logoutRequestXml, sp, _ct);

        // Act
        var result = await Fixture.Client.GetAsync($"/saml/logout?SAMLRequest={urlEncoded}", _ct);

        // Assert
        var logoutResponse = await ExtractSamlLogoutResponseFromPostAsync(result, _ct);
        logoutResponse.StatusCode.ShouldBe(SamlStatusCodes.Requester);
        logoutResponse.StatusMessage.ShouldBe("Request IssueInstant is in the future");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task logout_endpoint_when_issue_instant_is_too_old_should_return_error_response()
    {
        // Arrange
        var signingCert = CreateTestSigningCertificate(Data.FakeTimeProvider);
        var sp = Build.SamlServiceProvider(signingCertificate: signingCert);
        Fixture.ServiceProviders.Add(sp);
        await Fixture.InitializeAsync();

        var oldTime = Data.Now.AddMinutes(-10);
        var logoutRequestXml = Build.LogoutRequestXml(
            destination: new Uri($"{Fixture.Url()}/saml/logout"),
            issueInstant: oldTime,
            sessionIndex: "session123");
        var urlEncoded = await EncodeAndSignRequest(logoutRequestXml, sp, _ct);

        // Act
        var result = await Fixture.Client.GetAsync($"/saml/logout?SAMLRequest={urlEncoded}", _ct);

        // Assert
        var logoutResponse = await ExtractSamlLogoutResponseFromPostAsync(result, _ct);
        logoutResponse.StatusCode.ShouldBe(SamlStatusCodes.Requester);
        logoutResponse.StatusMessage.ShouldBe("Request has expired (IssueInstant too old)");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task logout_endpoint_when_destination_does_not_match_endpoint_should_return_error_response()
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
        var result = await Fixture.Client.GetAsync($"/saml/logout?SAMLRequest={urlEncoded}", _ct);

        // Assert
        var logoutResponse = await ExtractSamlLogoutResponseFromPostAsync(result, _ct);
        logoutResponse.StatusCode.ShouldBe(SamlStatusCodes.Requester);
        logoutResponse.StatusMessage.ShouldBe($"Invalid destination. Expected '{Fixture.Url()}/saml/logout'");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task logout_endpoint_when_service_provider_has_no_signing_certificates_configured_should_return_bad_request()
    {
        // Arrange
        var sp = Build.SamlServiceProvider();
        sp.SigningCertificates = [];
        Fixture.ServiceProviders.Add(sp);
        await Fixture.InitializeAsync();

        var logoutRequestXml = Build.LogoutRequestXml(
            destination: new Uri($"{Fixture.Url()}/saml/logout"),
            sessionIndex: "session123");
        var urlEncoded = await EncodeRequest(logoutRequestXml, _ct);

        // Act
        var result = await Fixture.Client.GetAsync($"/saml/logout?SAMLRequest={urlEncoded}", _ct);

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problemDetails = await result.Content.ReadFromJsonAsync<ProblemDetails>(_ct);
        problemDetails.ShouldNotBeNull();
        problemDetails.Detail.ShouldBe($"Service Provider '{sp.EntityId}' has no signing certificates configured and has sent a SAML logout request which requires signature validation");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task logout_endpoint_when_request_without_signature_received_should_return_error_response()
    {
        // Arrange
        var signingCert = CreateTestSigningCertificate(Data.FakeTimeProvider);
        var sp = Build.SamlServiceProvider(signingCertificate: signingCert);
        Fixture.ServiceProviders.Add(sp);
        await Fixture.InitializeAsync();

        // Create a logout request without a signature
        var logoutRequestXml = Build.LogoutRequestXml(
            destination: new Uri($"{Fixture.Url()}/saml/logout"),
            sessionIndex: "session123");
        var urlEncoded = await EncodeRequest(logoutRequestXml, _ct);

        // Act
        var result = await Fixture.Client.GetAsync($"/saml/logout?SAMLRequest={urlEncoded}", _ct);

        // Assert
        var logoutResponse = await ExtractSamlLogoutResponseFromPostAsync(result, _ct);
        logoutResponse.StatusCode.ShouldBe(SamlStatusCodes.Requester);
        logoutResponse.StatusMessage.ShouldBe("Missing signature parameter");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task logout_endpoint_when_not_on_or_after_is_in_past_should_return_error_response()
    {
        // Arrange
        var signingCert = CreateTestSigningCertificate(Data.FakeTimeProvider);
        var sp = Build.SamlServiceProvider(signingCertificate: signingCert);
        Fixture.ServiceProviders.Add(sp);
        await Fixture.InitializeAsync();

        var expiredTime = Data.Now.AddMinutes(-10);
        var logoutRequestXml = Build.LogoutRequestXml(
            notOnOrAfter: expiredTime,
            sessionIndex: "session123");
        var urlEncoded = await EncodeAndSignRequest(logoutRequestXml, sp, _ct);

        // Act
        var result = await Fixture.Client.GetAsync($"/saml/logout?SAMLRequest={urlEncoded}", _ct);

        // Assert
        var logoutResponse = await ExtractSamlLogoutResponseFromPostAsync(result, _ct);
        logoutResponse.StatusCode.ShouldBe(SamlStatusCodes.Requester);
        logoutResponse.StatusMessage.ShouldBe("Logout request expired (NotOnOrAfter is in the past)");
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
            destination: new Uri($"{Fixture.Url()}/saml/logout"),
            sessionIndex: "session123");
        var urlEncoded = await EncodeAndSignRequest(logoutRequestXml, sp, _ct);

        // Act
        var result = await Fixture.Client.GetAsync($"/saml/logout?SAMLRequest={urlEncoded}", _ct);

        // Assert
        var logoutResponse = await ExtractSamlLogoutResponseFromPostAsync(result, _ct);
        logoutResponse.StatusCode.ShouldBe(SamlStatusCodes.Success);
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
            destination: new Uri($"{Fixture.Url()}/saml/logout"));
        var urlEncoded = await EncodeAndSignRequest(logoutRequestXml, sp, _ct);

        // Act
        var result = await Fixture.Client.GetAsync($"/saml/logout?SAMLRequest={urlEncoded}", _ct);

        // Assert
        var logoutResponse = await ExtractSamlLogoutResponseFromPostAsync(result, _ct);
        logoutResponse.StatusCode.ShouldBe(SamlStatusCodes.Success);
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

        // Sign in a user first
        Fixture.UserToSignIn =
            new ClaimsPrincipal(new ClaimsIdentity([new Claim(JwtClaimTypes.Subject, "user-id")], "Test"));
        await Fixture.Client.GetAsync("/__signin", _ct);

        // Use a different session index than what was established
        var logoutRequestXml = Build.LogoutRequestXml(
            destination: new Uri($"{Fixture.Url()}/saml/logout"),
            sessionIndex: "wrong-session-index");
        var urlEncoded = await EncodeAndSignRequest(logoutRequestXml, sp, _ct);

        // Act
        var result = await Fixture.Client.GetAsync($"/saml/logout?SAMLRequest={urlEncoded}", _ct);

        // Assert
        var logoutResponse = await ExtractSamlLogoutResponseFromPostAsync(result, _ct);
        logoutResponse.StatusCode.ShouldBe(SamlStatusCodes.Success);
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

        // Sign in a user first
        Fixture.UserToSignIn =
            new ClaimsPrincipal(new ClaimsIdentity([new Claim(JwtClaimTypes.Subject, "user-id")], "Test"));
        await Fixture.Client.GetAsync("/__signin", _ct);

        // Perform logout to get correct session index from the response
        var sessionIndex = await PerformSigninAndExtractSessionIndex(sp);

        var logoutRequestXml = Build.LogoutRequestXml(
            destination: new Uri($"{Fixture.Url()}/saml/logout"),
            sessionIndex: sessionIndex);
        var urlEncoded = await EncodeAndSignRequest(logoutRequestXml, sp, _ct);

        // Act
        var result = await Fixture.Client.GetAsync($"/saml/logout?SAMLRequest={urlEncoded}", _ct);

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

        var sessionIndex = await PerformSigninAndExtractSessionIndex(sp);

        var logoutRequestXml = Build.LogoutRequestXml(
            destination: new Uri($"{Fixture.Url()}/saml/logout"),
            sessionIndex: sessionIndex);
        var urlEncoded = await EncodeAndSignRequest(logoutRequestXml, sp, _ct);

        // Act
        var result = await Fixture.Client.GetAsync($"/saml/logout?SAMLRequest={urlEncoded}", _ct);

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.OK); // Follows redirect

        // Verify user can no longer access protected resource and is redirected to login
        var finalProtectedResourceResult = await Fixture.Client.GetAsync("__protected-resource", _ct);
        finalProtectedResourceResult.StatusCode.ShouldBe(HttpStatusCode.OK);
        finalProtectedResourceResult.RequestMessage?.RequestUri?.AbsoluteUri.ShouldStartWith($"{Fixture.Url()}{Fixture.LoginUrl.ToString()}");
    }

    private static async Task<string> EncodeAndSignRequest(
        string xml,
        SamlServiceProvider sp,
        Ct ct = default)
    {
        var encoded = await EncodeRequest(xml, ct);

        // Sign the request using the SP's certificate
        var certificate = sp.SigningCertificates!.First();
        var (signature, sigAlg) = SignAuthNRequestRedirect(encoded, null, certificate);

        return $"{encoded}&SigAlg={Uri.EscapeDataString(sigAlg)}&Signature={Uri.EscapeDataString(signature)}";
    }

    private async Task<string> PerformSigninAndExtractSessionIndex(SamlServiceProvider samlServiceProvider)
    {
        var signinRequest = Build.AuthNRequestXml();
        var encoded = await EncodeAndSignRequest(signinRequest, samlServiceProvider, _ct);
        var signinResult = await Fixture.Client.GetAsync($"/saml/signin?SAMLRequest={encoded}", _ct);
        var samlResult = await ExtractSamlSuccessFromPostAsync(signinResult, _ct);
        if (string.IsNullOrWhiteSpace(samlResult.Assertion.AuthnStatement?.SessionIndex))
        {
            throw new InvalidOperationException("SAMLResult did not have a valid session index");
        }

        return samlResult.Assertion.AuthnStatement.SessionIndex;
    }
}
