// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using System.Globalization;
using System.Security.Claims;
using System.Xml;
using Duende.IdentityModel;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Endpoints.Results;
using Duende.IdentityServer.Licensing;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Saml;
using Duende.IdentityServer.Saml.Bindings;
using Duende.IdentityServer.Saml.Endpoints;
using Duende.IdentityServer.Saml.Endpoints.Results;
using Duende.IdentityServer.Saml.Models;
using Duende.IdentityServer.Saml.ResponseHandling;
using Duende.IdentityServer.Saml.Validation;
using Duende.IdentityServer.Stores;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using UnitTests.Common;

namespace UnitTests.Saml;

public sealed class SingleSignOnCallbackEndpointTests
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    private const string SpEntityId = "https://sp.example.com";
    private const string SpAcsUrl = "https://sp.example.com/acs";
    private const string IdpEntityId = "https://idp.example.com";
    private const string StateIdParameterName = "samlStateId";

    private static SamlAuthenticationState CreateState(
        string? acsUrl = SpAcsUrl) =>
        new()
        {
            ServiceProviderEntityId = SpEntityId,
            AssertionConsumerService = new IndexedEndpoint { Location = acsUrl ?? SpAcsUrl, Binding = SamlBinding.HttpPost },
        };

    private static SamlServiceProvider CreateSp(
        bool enabled = true,
        string? acsUrl = SpAcsUrl) =>
        new()
        {
            EntityId = SpEntityId,
            Enabled = enabled,
            AssertionConsumerServiceUrls = new HashSet<IndexedEndpoint> { new IndexedEndpoint { Location = acsUrl ?? SpAcsUrl, Binding = SamlBinding.HttpPost } }
        };

    private static ClaimsPrincipal CreateAuthenticatedUser(DateTimeOffset? authTime = null) =>
        new(new ClaimsIdentity(
            [
                new Claim(JwtClaimTypes.Subject, "user-123"),
                new Claim(JwtClaimTypes.AuthenticationTime,
                    (authTime ?? DateTimeOffset.UtcNow).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
                    ClaimValueTypes.Integer64)
            ],
            "TestAuth")); // non-empty authType → IsAuthenticated = true

    private static DefaultHttpContext CreateGetContext(
        Guid? stateId = null)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Get;

        if (stateId.HasValue)
        {
            context.Request.QueryString = new QueryString($"?{StateIdParameterName}={stateId}");
        }

        return context;
    }

    private static SingleSignOnCallbackEndpoint CreateEndpoint(
        SpySamlSigninStateStore? stateStore = null,
        MockUserSession? userSession = null,
        ISamlServiceProviderStore? spStore = null,
        StubResponseGenerator? responseGenerator = null,
        StubSaml2IssuerNameService? issuerNameService = null)
    {
        stateStore ??= new SpySamlSigninStateStore();
        userSession ??= new MockUserSession();
        spStore ??= new InMemorySamlServiceProviderStore([CreateSp()]);
        responseGenerator ??= new StubResponseGenerator();
        issuerNameService ??= new StubSaml2IssuerNameService(IdpEntityId);

        return new SingleSignOnCallbackEndpoint(
            stateStore,
            userSession,
            spStore,
            responseGenerator,
            issuerNameService,
            new MockServerUrls { Origin = "https://idp.example.com" },
            new TestEventService(),
            IdentityServerLicenseValidator.CreateForTests(),
            Options.Create(new IdentityServerOptions
            {
                UserInteraction = new UserInteractionOptions
                {
                    LoginUrl = "/account/login",
                    LoginReturnUrlParameter = "ReturnUrl"
                }
            }));
    }

    [Fact]
    public async Task ReturnsMethodNotAllowedForNonGetRequest()
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Post;
        var endpoint = CreateEndpoint();

        var result = await endpoint.ProcessAsync(context);

        result.ShouldBeOfType<StatusCodeResult>()
            .StatusCode.ShouldBe((int)System.Net.HttpStatusCode.MethodNotAllowed);
    }

    [Fact]
    public async Task ReturnsErrorWhenStateIdMissing()
    {
        var context = CreateGetContext(stateId: null);
        // No query string set — missing samlStateId
        context.Request.QueryString = QueryString.Empty;
        var endpoint = CreateEndpoint();

        var result = await endpoint.ProcessAsync(context);

        var frontChannel = result.ShouldBeOfType<Saml2FrontChannelResult>();
        frontChannel.Error.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task ReturnsErrorWhenStateIdNotValidGuid()
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Get;
        context.Request.QueryString = new QueryString($"?{StateIdParameterName}=not-a-guid");
        var endpoint = CreateEndpoint();

        var result = await endpoint.ProcessAsync(context);

        var frontChannel = result.ShouldBeOfType<Saml2FrontChannelResult>();
        frontChannel.Error.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task ReturnsErrorWhenStateNotFound()
    {
        var stateId = Guid.NewGuid();
        var stateStore = new SpySamlSigninStateStore(retrieveReturns: null);
        var context = CreateGetContext(stateId);
        var endpoint = CreateEndpoint(stateStore: stateStore);

        var result = await endpoint.ProcessAsync(context);

        var frontChannel = result.ShouldBeOfType<Saml2FrontChannelResult>();
        frontChannel.Error.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task RedirectsToLoginWhenUserNotAuthenticated()
    {
        var stateId = Guid.NewGuid();
        var stateStore = new SpySamlSigninStateStore(retrieveReturns: CreateState());
        var userSession = new MockUserSession { User = null! }; // null → not authenticated
        var context = CreateGetContext(stateId);
        var endpoint = CreateEndpoint(stateStore: stateStore, userSession: userSession);

        var result = await endpoint.ProcessAsync(context);

        var redirect = result.ShouldBeOfType<Saml2LoginRedirectResult>();
        // The stateId GUID appears literally in the URL (not encoded)
        redirect.RedirectUrl.ShouldContain(stateId.ToString());
    }

    [Fact]
    public async Task ReturnsErrorWhenServiceProviderNotFound()
    {
        var stateId = Guid.NewGuid();
        var stateStore = new SpySamlSigninStateStore(retrieveReturns: CreateState());
        var userSession = new MockUserSession { User = CreateAuthenticatedUser() };
        var spStore = new InMemorySamlServiceProviderStore([]);
        var context = CreateGetContext(stateId);
        var endpoint = CreateEndpoint(stateStore: stateStore, userSession: userSession, spStore: spStore);

        var result = await endpoint.ProcessAsync(context);

        var frontChannel = result.ShouldBeOfType<Saml2FrontChannelResult>();
        frontChannel.Error.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task ReturnsErrorWhenServiceProviderDisabled()
    {
        // InMemorySamlServiceProviderStore filters by Enabled, so a disabled SP
        // returns null — indistinguishable from "not found" at the store level.
        var stateId = Guid.NewGuid();
        var stateStore = new SpySamlSigninStateStore(retrieveReturns: CreateState());
        var userSession = new MockUserSession { User = CreateAuthenticatedUser() };
        var spStore = new InMemorySamlServiceProviderStore([CreateSp(enabled: false)]);
        var context = CreateGetContext(stateId);
        var endpoint = CreateEndpoint(stateStore: stateStore, userSession: userSession, spStore: spStore);

        var result = await endpoint.ProcessAsync(context);

        var frontChannel = result.ShouldBeOfType<Saml2FrontChannelResult>();
        frontChannel.Error.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task ReturnsErrorWhenAcsUrlNoLongerRegistered()
    {
        var stateId = Guid.NewGuid();
        // State has old ACS URL; SP now only has a different one
        var stateStore = new SpySamlSigninStateStore(retrieveReturns: CreateState(acsUrl: "https://sp.example.com/old-acs"));
        var userSession = new MockUserSession { User = CreateAuthenticatedUser() };
        var spStore = new InMemorySamlServiceProviderStore([CreateSp(acsUrl: SpAcsUrl)]); // different URL
        var context = CreateGetContext(stateId);
        var endpoint = CreateEndpoint(stateStore: stateStore, userSession: userSession, spStore: spStore);

        var result = await endpoint.ProcessAsync(context);

        var frontChannel = result.ShouldBeOfType<Saml2FrontChannelResult>();
        frontChannel.Error.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task ReturnsSuccessResponseForAuthenticatedUser()
    {
        var stateId = Guid.NewGuid();
        var stateStore = new SpySamlSigninStateStore(retrieveReturns: CreateState());
        var userSession = new MockUserSession { User = CreateAuthenticatedUser() };
        var spStore = new InMemorySamlServiceProviderStore([CreateSp()]);
        var responseGenerator = new StubResponseGenerator();
        var context = CreateGetContext(stateId);
        var endpoint = CreateEndpoint(stateStore: stateStore, userSession: userSession,
            spStore: spStore, responseGenerator: responseGenerator);

        var result = await endpoint.ProcessAsync(context);

        var frontChannel = result.ShouldBeOfType<Saml2FrontChannelResult>();
        frontChannel.Error.ShouldBeNull();
        frontChannel.Message.ShouldNotBeNull();
    }

    [Fact]
    public async Task DoesNotDeleteStateOnSuccessfulFlow()
    {
        var stateId = Guid.NewGuid();
        var stateStore = new SpySamlSigninStateStore(retrieveReturns: CreateState());
        var userSession = new MockUserSession { User = CreateAuthenticatedUser() };
        var spStore = new InMemorySamlServiceProviderStore([CreateSp()]);
        var context = CreateGetContext(stateId);
        var endpoint = CreateEndpoint(stateStore: stateStore, userSession: userSession, spStore: spStore);

        await endpoint.ProcessAsync(context);

        stateStore.RemoveCallCount.ShouldBe(0);
    }

    [Fact]
    public async Task TracksSpSessionOnSuccess()
    {
        var stateId = Guid.NewGuid();
        var stateStore = new SpySamlSigninStateStore(retrieveReturns: CreateState());
        var userSession = new MockUserSession { User = CreateAuthenticatedUser() };
        var spStore = new InMemorySamlServiceProviderStore([CreateSp()]);
        var context = CreateGetContext(stateId);
        var endpoint = CreateEndpoint(stateStore: stateStore, userSession: userSession, spStore: spStore);

        await endpoint.ProcessAsync(context);

        userSession.SamlSessions.ShouldHaveSingleItem();
        userSession.SamlSessions[0].EntityId.ShouldBe(SpEntityId);
    }

    [Fact]
    public async Task ReusesSessionIndexForExistingSPSession()
    {
        var stateId = Guid.NewGuid();
        const string ExistingSessionIndex = "existing-session-index";
        var stateStore = new SpySamlSigninStateStore(retrieveReturns: CreateState());
        var userSession = new MockUserSession
        {
            User = CreateAuthenticatedUser(),
            SamlSessions =
            [
                new SamlSpSessionData
                {
                    EntityId = SpEntityId,
                    SessionIndex = ExistingSessionIndex,
                    NameId = "user-123",
                    NameIdFormat = "urn:oasis:names:tc:SAML:1.1:nameid-format:unspecified"
                }
            ]
        };
        var spStore = new InMemorySamlServiceProviderStore([CreateSp()]);
        var context = CreateGetContext(stateId);
        var endpoint = CreateEndpoint(stateStore: stateStore, userSession: userSession, spStore: spStore);

        await endpoint.ProcessAsync(context);

        userSession.SamlSessions.ShouldHaveSingleItem();
        userSession.SamlSessions[0].SessionIndex.ShouldBe(ExistingSessionIndex);
    }

    [Fact]
    public async Task GeneratesNewSessionIndexForNewSPSession()
    {
        var stateId = Guid.NewGuid();
        var stateStore = new SpySamlSigninStateStore(retrieveReturns: CreateState());
        var userSession = new MockUserSession
        {
            User = CreateAuthenticatedUser(),
            SamlSessions = [] // no existing sessions
        };
        var spStore = new InMemorySamlServiceProviderStore([CreateSp()]);
        var context = CreateGetContext(stateId);
        var endpoint = CreateEndpoint(stateStore: stateStore, userSession: userSession, spStore: spStore);

        await endpoint.ProcessAsync(context);

        userSession.SamlSessions.ShouldHaveSingleItem();
        userSession.SamlSessions[0].SessionIndex.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task SetsIsIdpInitiatedOnValidatedRequestWhenStateIsIdpInitiated()
    {
        var stateId = Guid.NewGuid();
        var state = CreateState();
        state.IsIdpInitiated = true;
        var stateStore = new SpySamlSigninStateStore(retrieveReturns: state);
        var userSession = new MockUserSession { User = CreateAuthenticatedUser() };
        var responseGenerator = new StubResponseGenerator();
        var context = CreateGetContext(stateId);
        var endpoint = CreateEndpoint(stateStore: stateStore, userSession: userSession, responseGenerator: responseGenerator);

        await endpoint.ProcessAsync(context);

        responseGenerator.CapturedRequest.ShouldNotBeNull();
        responseGenerator.CapturedRequest.IsIdpInitiated.ShouldBeTrue();
    }

    [Fact]
    public async Task DoesNotSetIsIdpInitiatedOnValidatedRequestForSpInitiatedFlow()
    {
        var stateId = Guid.NewGuid();
        var stateStore = new SpySamlSigninStateStore(retrieveReturns: CreateState());
        var userSession = new MockUserSession { User = CreateAuthenticatedUser() };
        var responseGenerator = new StubResponseGenerator();
        var context = CreateGetContext(stateId);
        var endpoint = CreateEndpoint(stateStore: stateStore, userSession: userSession, responseGenerator: responseGenerator);

        await endpoint.ProcessAsync(context);

        responseGenerator.CapturedRequest.ShouldNotBeNull();
        responseGenerator.CapturedRequest.IsIdpInitiated.ShouldBeFalse();
    }

    [Fact]
    public async Task RedirectsToLoginWhenForceAuthnAndUserAuthenticatedBeforeStateCreated()
    {
        var stateId = Guid.NewGuid();
        var stateCreatedAt = DateTimeOffset.UtcNow;
        var userAuthTime = stateCreatedAt.AddMinutes(-5); // authenticated before the SAML flow began

        var state = CreateState();
        state.AuthnRequestData = new Duende.IdentityServer.Saml.StoredAuthnRequestData { ForceAuthn = true };
        state.CreatedUtc = stateCreatedAt;

        var stateStore = new SpySamlSigninStateStore(retrieveReturns: state);
        var userSession = new MockUserSession { User = CreateAuthenticatedUser(userAuthTime) };
        var context = CreateGetContext(stateId);
        var endpoint = CreateEndpoint(stateStore: stateStore, userSession: userSession);

        var result = await endpoint.ProcessAsync(context);

        result.ShouldBeOfType<Saml2LoginRedirectResult>();
    }

    [Fact]
    public async Task ReturnsResponseWhenForceAuthnAndUserReauthenticatedAfterStateCreated()
    {
        var stateId = Guid.NewGuid();
        var stateCreatedAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        var userAuthTime = stateCreatedAt.AddSeconds(30); // authenticated after the SAML flow began

        var state = CreateState();
        state.AuthnRequestData = new Duende.IdentityServer.Saml.StoredAuthnRequestData { ForceAuthn = true };
        state.CreatedUtc = stateCreatedAt;

        var stateStore = new SpySamlSigninStateStore(retrieveReturns: state);
        var userSession = new MockUserSession { User = CreateAuthenticatedUser(userAuthTime) };
        var context = CreateGetContext(stateId);
        var endpoint = CreateEndpoint(stateStore: stateStore, userSession: userSession);

        var result = await endpoint.ProcessAsync(context);

        var frontChannel = result.ShouldBeOfType<Saml2FrontChannelResult>();
        frontChannel.Error.ShouldBeNull();
        frontChannel.Message.ShouldNotBeNull();
    }

    [Fact]
    public async Task DoesNotCheckAuthTimeWhenForceAuthnIsFalse()
    {
        var stateId = Guid.NewGuid();
        var stateCreatedAt = DateTimeOffset.UtcNow;
        var userAuthTime = stateCreatedAt.AddMinutes(-5); // old auth, but ForceAuthn is false

        var state = CreateState();
        state.AuthnRequestData = new Duende.IdentityServer.Saml.StoredAuthnRequestData { ForceAuthn = false };
        state.CreatedUtc = stateCreatedAt;

        var stateStore = new SpySamlSigninStateStore(retrieveReturns: state);
        var userSession = new MockUserSession { User = CreateAuthenticatedUser(userAuthTime) };
        var context = CreateGetContext(stateId);
        var endpoint = CreateEndpoint(stateStore: stateStore, userSession: userSession);

        var result = await endpoint.ProcessAsync(context);

        var frontChannel = result.ShouldBeOfType<Saml2FrontChannelResult>();
        frontChannel.Error.ShouldBeNull();
    }

    [Fact]
    public async Task DoesNotCheckAuthTimeWhenAuthnRequestIsNull()
    {
        // IdP-initiated flow: no AuthnRequest in state
        var stateId = Guid.NewGuid();
        var stateCreatedAt = DateTimeOffset.UtcNow;
        var userAuthTime = stateCreatedAt.AddMinutes(-5);

        var state = CreateState();
        state.AuthnRequestData = null;
        state.CreatedUtc = stateCreatedAt;

        var stateStore = new SpySamlSigninStateStore(retrieveReturns: state);
        var userSession = new MockUserSession { User = CreateAuthenticatedUser(userAuthTime) };
        var context = CreateGetContext(stateId);
        var endpoint = CreateEndpoint(stateStore: stateStore, userSession: userSession);

        var result = await endpoint.ProcessAsync(context);

        var frontChannel = result.ShouldBeOfType<Saml2FrontChannelResult>();
        frontChannel.Error.ShouldBeNull();
    }

    [Fact]
    public async Task DenialState_AccessDenied_ReturnsErrorResponse()
    {
        var state = CreateState();
        state.DenialError = InteractionError.AccessDenied;
        state.DenialErrorDescription = "user cancelled";

        var stateStore = new SpySamlSigninStateStore(retrieveReturns: state);
        var responseGenerator = new StubResponseGenerator();
        var endpoint = CreateEndpoint(stateStore: stateStore, responseGenerator: responseGenerator);

        var stateId = Guid.NewGuid();
        var context = CreateGetContext(stateId);

        var result = await endpoint.ProcessAsync(context);

        var frontChannel = result.ShouldBeOfType<Saml2FrontChannelResult>();
        frontChannel.Error.ShouldBe("user cancelled");
    }

    [Fact]
    public async Task DenialState_InteractionRequired_MapsToNoPassive()
    {
        var state = CreateState();
        state.DenialError = InteractionError.InteractionRequired;

        var stateStore = new SpySamlSigninStateStore(retrieveReturns: state);
        var responseGenerator = new StubResponseGenerator();
        var endpoint = CreateEndpoint(stateStore: stateStore, responseGenerator: responseGenerator);

        var stateId = Guid.NewGuid();
        var context = CreateGetContext(stateId);

        var result = await endpoint.ProcessAsync(context);

        // StubResponseGenerator.CreateErrorResponse returns the message as Error
        var frontChannel = result.ShouldBeOfType<Saml2FrontChannelResult>();
        // No message was set (only status codes), so Error should be null
        frontChannel.Error.ShouldBeNull();
    }

    [Fact]
    public async Task DenialState_SpNotFound_ReturnsSpNotFoundError()
    {
        var state = CreateState();
        state.DenialError = InteractionError.AccessDenied;

        var stateStore = new SpySamlSigninStateStore(retrieveReturns: state);
        var spStore = new InMemorySamlServiceProviderStore([]); // empty — SP not found
        var endpoint = CreateEndpoint(stateStore: stateStore, spStore: spStore);

        var stateId = Guid.NewGuid();
        var context = CreateGetContext(stateId);

        var result = await endpoint.ProcessAsync(context);

        var frontChannel = result.ShouldBeOfType<Saml2FrontChannelResult>();
        frontChannel.Error.ShouldBe("Service provider not found");
    }

    [Fact]
    public async Task DenialState_SkipsUserAuthenticationCheck()
    {
        // Even with no authenticated user, denial should still produce an error response
        var state = CreateState();
        state.DenialError = InteractionError.AccessDenied;
        state.DenialErrorDescription = "denied";

        var stateStore = new SpySamlSigninStateStore(retrieveReturns: state);
        var userSession = new MockUserSession { User = null }; // not authenticated
        var responseGenerator = new StubResponseGenerator();
        var endpoint = CreateEndpoint(stateStore: stateStore, userSession: userSession, responseGenerator: responseGenerator);

        var stateId = Guid.NewGuid();
        var context = CreateGetContext(stateId);

        var result = await endpoint.ProcessAsync(context);

        // Should NOT redirect to login — should return error response
        var frontChannel = result.ShouldBeOfType<Saml2FrontChannelResult>();
        frontChannel.Error.ShouldBe("denied");
    }

    private sealed class StubResponseGenerator : ISaml2SsoResponseGenerator
    {
        public ValidatedAuthnRequest? CapturedRequest { get; private set; }

        public Task<Saml2FrontChannelResult> CreateResponse(ValidatedAuthnRequest validatedAuthnRequest, Ct ct)
        {
            CapturedRequest = validatedAuthnRequest;
            return Task.FromResult(new Saml2FrontChannelResult
            {
                Message = new OutboundSaml2Message
                {
                    Name = "SAMLResponse",
                    Xml = new XmlDocument().CreateElement("placeholder"),
                    Destination = SpAcsUrl,
                    Binding = "urn:oasis:names:tc:SAML:2.0:bindings:HTTP-POST"
                },
                GeneratedNameId = new NameId { Value = "user-123", Format = SamlConstants.NameIdentifierFormats.Unspecified }
            });
        }

        public Task<Saml2FrontChannelResult> CreateErrorResponse(ValidatedAuthnRequest validatedAuthnRequest, Saml2InteractionResponse interactionResponse, Ct ct) =>
            Task.FromResult(new Saml2FrontChannelResult { Error = interactionResponse.Message });
    }
}
