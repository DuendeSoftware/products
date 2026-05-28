// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using System.Net;
using System.Security.Claims;
using System.Web;
using Duende.IdentityModel;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Saml;
using Duende.IdentityServer.Saml.Models;
using Duende.IdentityServer.Stores;
using static Duende.IdentityServer.IntegrationTests.Endpoints.Saml.SamlTestHelpers;

namespace Duende.IdentityServer.IntegrationTests.Endpoints.Saml;

public class SamlSingleLogoutCallbackEndpointTests
{
    private const string Category = "SAML single logout callback endpoint";

    private readonly Ct _ct = TestContext.Current.CancellationToken;

    private SamlFixture Fixture = new();

    private SamlData Data => Fixture.Data;

    private SamlDataBuilder Build => Fixture.Builder;

    [Fact]
    [Trait("Category", Category)]
    public async Task callback_with_post_method_should_return_method_not_allowed()
    {
        // Arrange
        Fixture.ServiceProviders.Add(Build.SamlServiceProvider());
        await Fixture.InitializeAsync();

        // Act
        var result = await Fixture.Client.PostAsync("/Saml2/SLO/Callback", new StringContent(""), _ct);

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.MethodNotAllowed);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task callback_with_missing_logout_id_should_redirect_to_error_page()
    {
        // Arrange
        Fixture.ServiceProviders.Add(Build.SamlServiceProvider());
        await Fixture.InitializeAsync();

        // Act
        var result = await Fixture.NonRedirectingClient.GetAsync("/Saml2/SLO/Callback", _ct);

        // Assert
        var errorMessage = await Fixture.GetErrorFromRedirect(result, _ct);
        errorMessage.ShouldBe("Missing or invalid SAML logout state identifier");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task callback_with_invalid_logout_id_should_redirect_to_error_page()
    {
        // Arrange
        Fixture.ServiceProviders.Add(Build.SamlServiceProvider());
        await Fixture.InitializeAsync();

        // Act
        var result = await Fixture.NonRedirectingClient.GetAsync("/Saml2/SLO/Callback?logoutId=invalid", _ct);

        // Assert
        var errorMessage = await Fixture.GetErrorFromRedirect(result, _ct);
        errorMessage.ShouldBe("SAML logout state not found or expired");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task callback_with_valid_logout_id_should_return_success_response()
    {
        // Arrange
        var sp = Build.SamlServiceProvider();
        Fixture.ServiceProviders.Add(sp);
        await Fixture.InitializeAsync();

        // Store a logout message as if SP-initiated logout occurred
        var logoutMessage = new LogoutMessage
        {
            SubjectId = "user123",
            SessionId = "session456",
            SamlServiceProviderEntityId = sp.EntityId,
            SamlLogoutRequestId = "_abc123",
            SamlRelayState = null
        };
        var messageStore = Fixture.Get<IMessageStore<LogoutMessage>>();
        var logoutId = await messageStore.WriteAsync(new Message<LogoutMessage>(logoutMessage, DateTime.UtcNow), _ct);

        // Act
        var result = await Fixture.NonRedirectingClient.GetAsync($"/Saml2/SLO/Callback?logoutId={logoutId}", _ct);

        // Assert
        var samlResponse = SamlTestHelpers.ExtractSamlLogoutResponseFromRedirect(result);
        samlResponse.StatusCode.ShouldBe(SamlStatusCodes.Success);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task callback_should_include_relay_state_if_present()
    {
        // Arrange
        var sp = Build.SamlServiceProvider();
        Fixture.ServiceProviders.Add(sp);
        await Fixture.InitializeAsync();

        var logoutMessage = new LogoutMessage
        {
            SubjectId = "user123",
            SessionId = "session456",
            SamlServiceProviderEntityId = sp.EntityId,
            SamlLogoutRequestId = "_abc123",
            SamlRelayState = "mystate123"
        };
        var messageStore = Fixture.Get<IMessageStore<LogoutMessage>>();
        var logoutId = await messageStore.WriteAsync(new Message<LogoutMessage>(logoutMessage, DateTime.UtcNow), _ct);

        // Act
        var result = await Fixture.NonRedirectingClient.GetAsync($"/Saml2/SLO/Callback?logoutId={logoutId}", _ct);

        // Assert
        var response = SamlTestHelpers.ExtractSamlLogoutResponseFromRedirect(result);
        response.RelayState.ShouldBe(logoutMessage.SamlRelayState);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task callback_with_disabled_service_provider_should_redirect_to_error_page()
    {
        // Arrange
        var sp = Build.SamlServiceProvider();
        sp.Enabled = false;
        Fixture.ServiceProviders.Add(sp);
        await Fixture.InitializeAsync();

        var logoutMessage = new LogoutMessage
        {
            SubjectId = "user123",
            SessionId = "session456",
            SamlServiceProviderEntityId = sp.EntityId,
            SamlLogoutRequestId = "_abc123"
        };
        var messageStore = Fixture.Get<IMessageStore<LogoutMessage>>();
        var logoutId = await messageStore.WriteAsync(new Message<LogoutMessage>(logoutMessage, DateTime.UtcNow), _ct);

        // Act
        var result = await Fixture.NonRedirectingClient.GetAsync($"/Saml2/SLO/Callback?logoutId={logoutId}", _ct);

        // Assert
        var errorMessage = await Fixture.GetErrorFromRedirect(result, _ct);
        errorMessage.ShouldBe("SAML service provider not found");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task callback_returns_success_when_all_sps_responded_successfully()
    {
        // Arrange
        var sp = Build.SamlServiceProvider();
        Fixture.ServiceProviders.Add(sp);
        await Fixture.InitializeAsync();

        // Store the logout message first to get the logoutId
        var logoutMessage = new LogoutMessage
        {
            SamlServiceProviderEntityId = sp.EntityId,
            SamlLogoutRequestId = "_abc123"
        };
        var messageStore = Fixture.Get<IMessageStore<LogoutMessage>>();
        var logoutId = await messageStore.WriteAsync(new Message<LogoutMessage>(logoutMessage, DateTime.UtcNow), _ct);

        // Store a logout session keyed by the actual logoutId, with one expected SP response recorded as success
        var sessionStore = Fixture.Get<ISamlLogoutSessionStore>();
        var session = new SamlLogoutSession
        {
            LogoutId = logoutId,
            ExpectedResponses = new Dictionary<string, ExpectedSpLogout>
            {
                ["_req-sp2"] = new("https://sp2.example.com")
            },
            CreatedUtc = Data.Now,
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(5)
        };
        await sessionStore.StoreAsync(session, _ct);
        await sessionStore.TryRecordResponseAsync("_req-sp2", "https://sp2.example.com", true, _ct);

        // Act
        var result = await Fixture.NonRedirectingClient.GetAsync($"/Saml2/SLO/Callback?logoutId={logoutId}", _ct);

        // Assert
        var samlResponse = ExtractSamlLogoutResponseFromRedirect(result);
        samlResponse.StatusCode.ShouldBe(SamlStatusCodes.Success);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task callback_returns_partial_logout_when_sp_response_missing()
    {
        // Arrange
        var sp = Build.SamlServiceProvider();
        Fixture.ServiceProviders.Add(sp);
        await Fixture.InitializeAsync();

        // Store a logout message and get the logoutId
        var logoutMessage = new LogoutMessage
        {
            SamlServiceProviderEntityId = sp.EntityId,
            SamlLogoutRequestId = "_abc123"
        };
        var messageStore = Fixture.Get<IMessageStore<LogoutMessage>>();
        var logoutId = await messageStore.WriteAsync(new Message<LogoutMessage>(logoutMessage, DateTime.UtcNow), _ct);

        // Store a logout session with a pending (unrecorded) SP response
        var sessionStore = Fixture.Get<ISamlLogoutSessionStore>();
        var session = new SamlLogoutSession
        {
            LogoutId = logoutId,
            ExpectedResponses = new Dictionary<string, ExpectedSpLogout>
            {
                ["_req-sp2"] = new("https://sp2.example.com")
            },
            CreatedUtc = Data.Now,
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(5)
        };
        await sessionStore.StoreAsync(session, _ct);
        // Deliberately NOT recording a response for _req-sp2

        // Act
        var result = await Fixture.NonRedirectingClient.GetAsync($"/Saml2/SLO/Callback?logoutId={logoutId}", _ct);

        // Assert
        var samlResponse = ExtractSamlLogoutResponseFromRedirect(result);
        samlResponse.StatusCode.ShouldBe(SamlStatusCodes.Success);
        samlResponse.SubStatusCode.ShouldBe(SamlStatusCodes.PartialLogout);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task callback_returns_partial_logout_when_sp_reported_failure()
    {
        // Arrange
        var sp = Build.SamlServiceProvider();
        Fixture.ServiceProviders.Add(sp);
        await Fixture.InitializeAsync();

        var logoutMessage = new LogoutMessage
        {
            SamlServiceProviderEntityId = sp.EntityId,
            SamlLogoutRequestId = "_abc123"
        };
        var messageStore = Fixture.Get<IMessageStore<LogoutMessage>>();
        var logoutId = await messageStore.WriteAsync(new Message<LogoutMessage>(logoutMessage, DateTime.UtcNow), _ct);

        var sessionStore = Fixture.Get<ISamlLogoutSessionStore>();
        var session = new SamlLogoutSession
        {
            LogoutId = logoutId,
            ExpectedResponses = new Dictionary<string, ExpectedSpLogout>
            {
                ["_req-sp2"] = new("https://sp2.example.com")
            },
            CreatedUtc = Data.Now,
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(5)
        };
        await sessionStore.StoreAsync(session, _ct);
        await sessionStore.TryRecordResponseAsync("_req-sp2", "https://sp2.example.com", false, _ct);

        // Act
        var result = await Fixture.NonRedirectingClient.GetAsync($"/Saml2/SLO/Callback?logoutId={logoutId}", _ct);

        // Assert
        var samlResponse = ExtractSamlLogoutResponseFromRedirect(result);
        samlResponse.StatusCode.ShouldBe(SamlStatusCodes.Success);
        samlResponse.SubStatusCode.ShouldBe(SamlStatusCodes.PartialLogout);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task callback_returns_partial_logout_when_no_session_in_store()
    {
        // Arrange — no session stored, simulating lost/expired tracking state
        var sp = Build.SamlServiceProvider();
        Fixture.ServiceProviders.Add(sp);
        await Fixture.InitializeAsync();

        var logoutMessage = new LogoutMessage
        {
            SamlServiceProviderEntityId = sp.EntityId,
            SamlLogoutRequestId = "_abc123"
        };
        var messageStore = Fixture.Get<IMessageStore<LogoutMessage>>();
        var logoutId = await messageStore.WriteAsync(new Message<LogoutMessage>(logoutMessage, DateTime.UtcNow), _ct);

        // Act
        var result = await Fixture.NonRedirectingClient.GetAsync($"/Saml2/SLO/Callback?logoutId={logoutId}", _ct);

        // Assert
        var samlResponse = ExtractSamlLogoutResponseFromRedirect(result);
        samlResponse.StatusCode.ShouldBe(SamlStatusCodes.Success);
        samlResponse.SubStatusCode.ShouldBe(SamlStatusCodes.PartialLogout);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task callback_cleans_up_session_from_store()
    {
        // Arrange
        var sp = Build.SamlServiceProvider();
        Fixture.ServiceProviders.Add(sp);
        await Fixture.InitializeAsync();

        var logoutMessage = new LogoutMessage
        {
            SamlServiceProviderEntityId = sp.EntityId,
            SamlLogoutRequestId = "_abc123"
        };
        var messageStore = Fixture.Get<IMessageStore<LogoutMessage>>();
        var logoutId = await messageStore.WriteAsync(new Message<LogoutMessage>(logoutMessage, DateTime.UtcNow), _ct);

        var sessionStore = Fixture.Get<ISamlLogoutSessionStore>();
        var session = new SamlLogoutSession
        {
            LogoutId = logoutId,
            ExpectedResponses = new Dictionary<string, ExpectedSpLogout>
            {
                ["_req-sp2"] = new("https://sp2.example.com")
            },
            CreatedUtc = Data.Now,
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(5)
        };
        await sessionStore.StoreAsync(session, _ct);
        await sessionStore.TryRecordResponseAsync("_req-sp2", "https://sp2.example.com", true, _ct);

        // Act
        await Fixture.NonRedirectingClient.GetAsync($"/Saml2/SLO/Callback?logoutId={logoutId}", _ct);

        // Assert — session should be removed from the store
        var remaining = await sessionStore.GetByLogoutIdAsync(logoutId, _ct);
        remaining.ShouldBeNull();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task single_sp_logout_flow_returns_success()
    {
        // Arrange — sign in and SSO to a single SP
        var signingCert = CreateTestSigningCertificate(Data.FakeTimeProvider);
        var sp = Build.SamlServiceProvider(signingCertificate: signingCert);
        Fixture.ServiceProviders.Add(sp);
        await Fixture.InitializeAsync();

        Fixture.UserToSignIn =
            new ClaimsPrincipal(new ClaimsIdentity([new Claim(JwtClaimTypes.Subject, "user-id")], "Test"));
        await Fixture.Client.GetAsync("/__signin", _ct);

        var (sessionIndex, nameId) = await PerformSigninAndExtractSessionInfo(Fixture, sp, _ct);

        // SP sends LogoutRequest → redirects to logout page
        var logoutRequestXml = Build.LogoutRequestXml(
            destination: new Uri($"{Fixture.Url()}/Saml2/SLO"),
            sessionIndex: sessionIndex,
            nameId: nameId);
        var urlEncoded = await EncodeAndSignRequest(logoutRequestXml, sp, _ct);

        // The pipeline's OnLogout handler calls GetLogoutContextAsync (populating the
        // LogoutNotificationContext with SamlLogoutId) and signs the user out
        var logoutResult = await Fixture.Client.GetAsync($"/Saml2/SLO?SAMLRequest={urlEncoded}", _ct);
        logoutResult.StatusCode.ShouldBe(HttpStatusCode.OK);

        // The pipeline stores the LogoutRequest from GetLogoutContextAsync
        var logoutContext = Fixture.Pipeline.LogoutRequest;
        logoutContext.ShouldNotBeNull();
        logoutContext.SignOutIFrameUrl.ShouldNotBeNullOrWhiteSpace();
        logoutContext.PostLogoutRedirectUri.ShouldNotBeNullOrWhiteSpace();

        // Hit the end session callback (what the iframe would do) — this triggers
        // ValidateCallbackAsync which populates the SamlLogoutSession store
        await Fixture.Client.GetAsync(logoutContext.SignOutIFrameUrl!, _ct);

        // Extract the logoutId from the PostLogoutRedirectUri
        var postLogoutUri = logoutContext.PostLogoutRedirectUri!;
        var fullUri = new Uri(new Uri(Fixture.Url()), postLogoutUri);
        var callbackQuery = HttpUtility.ParseQueryString(fullUri.Query);
        var logoutId = callbackQuery["logoutId"];
        logoutId.ShouldNotBeNullOrWhiteSpace();

        // Act — hit the SAML SLO callback endpoint
        var callbackResult = await Fixture.NonRedirectingClient.GetAsync(
            $"/Saml2/SLO/Callback?logoutId={logoutId}", _ct);

        // Assert — single SP, no other SPs to notify, should be Success
        var samlResponse = ExtractSamlLogoutResponseFromRedirect(callbackResult);
        samlResponse.StatusCode.ShouldBe(SamlStatusCodes.Success);
        samlResponse.SubStatusCode.ShouldBeNull();
    }

}
