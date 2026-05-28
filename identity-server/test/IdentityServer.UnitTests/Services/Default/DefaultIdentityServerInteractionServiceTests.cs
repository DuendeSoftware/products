// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Saml;
using Duende.IdentityServer.Saml.Models;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Stores;
using Duende.IdentityServer.Validation;
using Microsoft.Extensions.DependencyInjection;
using UnitTests.Common;

namespace UnitTests.Services.Default;

public class DefaultIdentityServerInteractionServiceTests
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    private DefaultIdentityServerInteractionService _subject;

    private IdentityServerOptions _options = new IdentityServerOptions();
    private MockHttpContextAccessor _mockMockHttpContextAccessor;
    private MockMessageStore<LogoutNotificationContext> _mockEndSessionStore = new MockMessageStore<LogoutNotificationContext>();
    private MockMessageStore<LogoutMessage> _mockLogoutMessageStore = new MockMessageStore<LogoutMessage>();
    private MockMessageStore<ErrorMessage> _mockErrorMessageStore = new MockMessageStore<ErrorMessage>();
    private MockConsentMessageStore _mockConsentStore = new MockConsentMessageStore();
    private MockPersistedGrantService _mockPersistedGrantService = new MockPersistedGrantService();
    private MockUserSession _mockUserSession = new MockUserSession();
    private MockReturnUrlParser _mockReturnUrlParser = new MockReturnUrlParser();
    private MockServerUrls _mockServerUrls = new MockServerUrls();
    private List<Client> _clients = [];
    private InMemoryClientStore _mockClientStore;

    private ResourceValidationResult _resourceValidationResult;

    public DefaultIdentityServerInteractionServiceTests()
    {
        _mockClientStore = new InMemoryClientStore(_clients);
        _mockMockHttpContextAccessor = new MockHttpContextAccessor(_options, _mockUserSession, _mockEndSessionStore,
            _mockServerUrls, configureServices: services => services.AddSingleton<IClientStore>(_mockClientStore));

        _subject = new DefaultIdentityServerInteractionService(
            _options,
            new FakeTimeProvider(),
            _mockMockHttpContextAccessor,
            _mockLogoutMessageStore,
            _mockErrorMessageStore,
            _mockConsentStore,
            _mockPersistedGrantService,
            _mockUserSession,
            _mockReturnUrlParser,
            TestLogger.Create<DefaultIdentityServerInteractionService>()
        );

        _resourceValidationResult = new ResourceValidationResult();
        _resourceValidationResult.Resources.IdentityResources.Add(new IdentityResources.OpenId());
        _resourceValidationResult.ParsedScopes.Add(new ParsedScopeValue("openid"));
    }

    [Fact]
    public async Task GetLogoutContextAsync_valid_session_and_logout_id_should_not_provide_signout_iframe()
    {
        // for this, we're just confirming that since the session has changed, there's not use in doing the iframe and thsu SLO
        _mockUserSession.SessionId = null;
        _mockLogoutMessageStore.Messages.Add("id", new Message<LogoutMessage>(new LogoutMessage() { SessionId = "session" }));

        var context = await _subject.GetLogoutContextAsync("id", _ct);

        context.SignOutIFrameUrl.ShouldBeNull();
    }

    [Fact]
    public async Task GetLogoutContextAsync_valid_session_with_client_without_front_channel_logout_uri_and_no_logout_id_should_not_provide_iframe()
    {
        _clients.Add(new Client
        {
            ClientId = "foo"
        });
        _mockUserSession.SessionId = "session";
        _mockUserSession.User = new IdentityServerUser("123").CreatePrincipal();

        var context = await _subject.GetLogoutContextAsync(null, _ct);

        context.SignOutIFrameUrl.ShouldBeNull();
    }

    [Fact]
    public async Task GetLogoutContextAsync_valid_session_with_client_with_front_channel_logout_uri_and_no_logout_id_should_provide_iframe()
    {
        _mockUserSession.Clients.Add("foo");
        _clients.Add(new Client
        {
            ClientId = "foo",
            FrontChannelLogoutUri = "https://client.com/logout",
        });
        _mockUserSession.SessionId = "session";
        _mockUserSession.User = new IdentityServerUser("123").CreatePrincipal();

        var context = await _subject.GetLogoutContextAsync(null, _ct);

        context.SignOutIFrameUrl.ShouldNotBeNull();
    }

    [Fact]
    public async Task GetLogoutContextAsync_without_session_should_not_provide_iframe()
    {
        _mockUserSession.SessionId = null;
        _mockLogoutMessageStore.Messages.Add("id", new Message<LogoutMessage>(new LogoutMessage()));

        var context = await _subject.GetLogoutContextAsync("id", _ct);

        context.SignOutIFrameUrl.ShouldBeNull();
    }

    [Fact]
    public async Task CreateLogoutContextAsync_without_session_should_not_create_session()
    {
        var context = await _subject.CreateLogoutContextAsync(_ct);

        context.ShouldBeNull();
        _mockLogoutMessageStore.Messages.ShouldBeEmpty();
    }

    [Fact]
    public async Task CreateLogoutContextAsync_with_session_should_create_session()
    {
        _mockUserSession.Clients.Add("foo");
        _mockUserSession.User = new IdentityServerUser("123").CreatePrincipal();
        _mockUserSession.SessionId = "session";

        var context = await _subject.CreateLogoutContextAsync(_ct);

        context.ShouldNotBeNull();
        _mockLogoutMessageStore.Messages.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task GrantConsentAsync_should_throw_if_granted_and_no_subject()
    {
        var act = () => _subject.GrantConsentAsync(
            new AuthorizationRequest(),
            new ConsentResponse() { ScopesValuesConsented = new[] { "openid" } },
            _ct,
            null);

        var exception = await act.ShouldThrowAsync<ArgumentNullException>();
        exception.ParamName!.ShouldMatch(".*subject.*");
    }

    [Fact]
    public async Task GrantConsentAsync_should_allow_deny_for_anonymous_users()
    {
        var req = new AuthorizationRequest()
        {
            Client = new Client { ClientId = "client" },
            ValidatedResources = _resourceValidationResult
        };
        await _subject.GrantConsentAsync(req, new ConsentResponse { Error = InteractionError.AccessDenied }, _ct, null);
    }

    [Fact]
    public async Task GrantConsentAsync_should_use_current_subject_and_create_message()
    {
        _mockUserSession.User = new IdentityServerUser("bob").CreatePrincipal();

        var req = new AuthorizationRequest()
        {
            Client = new Client { ClientId = "client" },
            ValidatedResources = _resourceValidationResult
        };
        await _subject.GrantConsentAsync(req, new ConsentResponse(), _ct, null);

        _mockConsentStore.Messages.ShouldNotBeEmpty();
        var consentRequest = new ConsentRequest(req, "bob");
        _mockConsentStore.Messages.First().Key.ShouldBe(consentRequest.Id);
    }

    [Fact]
    public async Task CreateLogoutContextAsync_with_saml_sessions_only_should_create_context()
    {
        _mockUserSession.User = new IdentityServerUser("123").CreatePrincipal();
        _mockUserSession.SessionId = "session";
        _mockUserSession.SamlSessions.Add(new SamlSpSessionData
        {
            EntityId = "https://sp1.example.com",
            SessionIndex = "idx1",
            NameId = "user123"
        });

        var context = await _subject.CreateLogoutContextAsync(_ct);

        context.ShouldNotBeNull();
        _mockLogoutMessageStore.Messages.ShouldNotBeEmpty();
        var message = _mockLogoutMessageStore.Messages[context];
        message.Data.SamlSessions.ShouldNotBeNull();
        message.Data.SamlSessions.Select(s => s.EntityId).ShouldContain("https://sp1.example.com");
        message.Data.ClientIds.ShouldBeEmpty();
    }

    [Fact]
    public async Task CreateLogoutContextAsync_with_mixed_sessions_should_include_both()
    {
        _mockUserSession.User = new IdentityServerUser("123").CreatePrincipal();
        _mockUserSession.SessionId = "session";
        _mockUserSession.Clients.Add("client1");
        _mockUserSession.Clients.Add("client2");
        _mockUserSession.SamlSessions.Add(new SamlSpSessionData
        {
            EntityId = "https://sp1.example.com",
            SessionIndex = "idx1",
            NameId = "user123"
        });
        _mockUserSession.SamlSessions.Add(new SamlSpSessionData
        {
            EntityId = "https://sp2.example.com",
            SessionIndex = "idx2",
            NameId = "user123"
        });

        var context = await _subject.CreateLogoutContextAsync(_ct);

        context.ShouldNotBeNull();
        _mockLogoutMessageStore.Messages.ShouldNotBeEmpty();
        var message = _mockLogoutMessageStore.Messages[context];

        message.Data.ClientIds.ShouldNotBeNull();
        message.Data.ClientIds.Count().ShouldBe(2);
        message.Data.ClientIds.ShouldContain("client1");
        message.Data.ClientIds.ShouldContain("client2");

        message.Data.SamlSessions.ShouldNotBeNull();
        message.Data.SamlSessions.Count().ShouldBe(2);
        message.Data.SamlSessions.Select(s => s.EntityId).ShouldContain("https://sp1.example.com");
        message.Data.SamlSessions.Select(s => s.EntityId).ShouldContain("https://sp2.example.com");
    }

    [Fact]
    public async Task CreateLogoutContextAsync_with_oidc_sessions_should_not_populate_saml()
    {
        _mockUserSession.User = new IdentityServerUser("123").CreatePrincipal();
        _mockUserSession.SessionId = "session";
        _mockUserSession.Clients.Add("client1");

        var context = await _subject.CreateLogoutContextAsync(_ct);

        context.ShouldNotBeNull();
        _mockLogoutMessageStore.Messages.ShouldNotBeEmpty();
        var message = _mockLogoutMessageStore.Messages[context];
        message.Data.ClientIds?.ShouldContain("client1");

        message.Data.SamlSessions.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetAuthenticationContextAsync_returns_oidc_context_when_return_url_parses()
    {
        var client = new Client { ClientId = "oidc-client", ClientName = "OIDC Client" };
        var authzRequest = new AuthorizationRequest { Client = client, IdP = "google" };
        _mockReturnUrlParser.AuthorizationRequestResult = authzRequest;

        var result = await _subject.GetAuthenticationContextAsync("/connect/authorize/callback?authzId=123", _ct);

        result.ShouldNotBeNull();
        result.ShouldBeOfType<AuthorizationRequest>();
        result.Application.ShouldBeSameAs(client);
        result.IdP.ShouldBe("google");
    }

    [Fact]
    public async Task GetAuthenticationContextAsync_returns_saml_context_when_oidc_returns_null()
    {
        var sp = new SamlServiceProvider { EntityId = "https://sp.example.com", DisplayName = "Test SP" };
        var samlRequest = new SamlAuthenticationContext { ServiceProvider = sp };
        _mockReturnUrlParser.ParseResult = samlRequest;

        // The mock returns whatever ParseResult is set to, regardless of URL
        var result = await _subject.GetAuthenticationContextAsync("/any-url", _ct);

        result.ShouldNotBeNull();
        result.ShouldBeOfType<SamlAuthenticationContext>();
        result.Application.ShouldBeSameAs(sp);
        result.Application.Identifier.ShouldBe("https://sp.example.com");
    }

    [Fact]
    public async Task GetAuthenticationContextAsync_returns_null_when_no_context_found()
    {
        _mockReturnUrlParser.ParseResult = null;

        var result = await _subject.GetAuthenticationContextAsync(null, _ct);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetAuthenticationContextAsync_prefers_oidc_over_saml()
    {
        // The unified parser returns the first match — OIDC parser is registered first.
        // This test verifies that an OIDC result is returned as AuthorizationRequest.
        var client = new Client { ClientId = "oidc-client" };
        var authzRequest = new AuthorizationRequest { Client = client };
        _mockReturnUrlParser.ParseResult = authzRequest;

        var result = await _subject.GetAuthenticationContextAsync("/connect/authorize/callback", _ct);

        result.ShouldBeOfType<AuthorizationRequest>();
        result!.Application.Identifier.ShouldBe("oidc-client");
    }

    [Fact]
    public async Task GetAuthenticationContextAsync_pattern_matches_to_protocol_specific_type()
    {
        var client = new Client { ClientId = "oidc-client" };
        var authzRequest = new AuthorizationRequest { Client = client, LoginHint = "alice" };
        _mockReturnUrlParser.AuthorizationRequestResult = authzRequest;

        var result = await _subject.GetAuthenticationContextAsync("/authorize", _ct);

        // Demonstrates the intended consumption pattern.
        var identifier = result switch
        {
            AuthorizationRequest oidc => oidc.Client.ClientId,
            SamlAuthenticationContext saml => saml.ServiceProvider.EntityId,
            _ => "unknown"
        };

        identifier.ShouldBe("oidc-client");
    }

    [Fact]
    public async Task GetLogoutContextAsync_saml_logout_should_append_logoutId_to_PostLogoutRedirectUri()
    {
        _mockLogoutMessageStore.Messages.Add("saml-logout-id", new Message<LogoutMessage>(new LogoutMessage
        {
            SessionId = "session",
            SamlServiceProviderEntityId = "https://sp.example.com",
            PostLogoutRedirectUri = "https://idp.example.com/saml/slo/callback"
        }));
        _mockUserSession.SessionId = "session";

        var context = await _subject.GetLogoutContextAsync("saml-logout-id", _ct);

        context.PostLogoutRedirectUri.ShouldNotBeNull();
        context.PostLogoutRedirectUri.ShouldContain("logoutId=saml-logout-id");
    }

    [Fact]
    public async Task GetLogoutContextAsync_saml_logout_uses_configured_logoutId_parameter_name()
    {
        _options.UserInteraction.LogoutIdParameter = "custom_logout_id";
        _mockLogoutMessageStore.Messages.Add("saml-logout-id", new Message<LogoutMessage>(new LogoutMessage
        {
            SessionId = "session",
            SamlServiceProviderEntityId = "https://sp.example.com",
            PostLogoutRedirectUri = "https://idp.example.com/saml/slo/callback"
        }));
        _mockUserSession.SessionId = "session";

        var context = await _subject.GetLogoutContextAsync("saml-logout-id", _ct);

        context.PostLogoutRedirectUri.ShouldNotBeNull();
        context.PostLogoutRedirectUri.ShouldContain("custom_logout_id=saml-logout-id");
        context.PostLogoutRedirectUri.ShouldNotContain("logoutId=");
    }

    [Fact]
    public async Task GetLogoutContextAsync_non_saml_logout_should_not_modify_PostLogoutRedirectUri()
    {
        _mockLogoutMessageStore.Messages.Add("oidc-logout-id", new Message<LogoutMessage>(new LogoutMessage
        {
            SessionId = "session",
            PostLogoutRedirectUri = "https://client.example.com/signout-callback"
        }));
        _mockUserSession.SessionId = "session";

        var context = await _subject.GetLogoutContextAsync("oidc-logout-id", _ct);

        context.PostLogoutRedirectUri.ShouldBe("https://client.example.com/signout-callback");
    }

    [Fact]
    public async Task GetLogoutContextAsync_saml_logout_without_PostLogoutRedirectUri_should_not_throw()
    {
        _mockLogoutMessageStore.Messages.Add("saml-logout-id", new Message<LogoutMessage>(new LogoutMessage
        {
            SessionId = "session",
            SamlServiceProviderEntityId = "https://sp.example.com",
            PostLogoutRedirectUri = null
        }));
        _mockUserSession.SessionId = "session";

        var context = await _subject.GetLogoutContextAsync("saml-logout-id", _ct);

        context.PostLogoutRedirectUri.ShouldBeNull();
    }

    [Fact]
    public async Task DenyAuthenticationAsync_WithAuthorizationRequest_WritesConsentDenial()
    {
        var request = new AuthorizationRequest
        {
            Client = new Client { ClientId = "client1" },
            ValidatedResources = _resourceValidationResult
        };

        await _subject.DenyAuthenticationAsync(request, InteractionError.AccessDenied, _ct, "user cancelled");

        var stored = _mockConsentStore.Messages.Values.Single();
        stored.Data.Error.ShouldBe(InteractionError.AccessDenied);
        stored.Data.ErrorDescription.ShouldBe("user cancelled");
    }

    [Fact]
    public async Task DenyAuthenticationAsync_WithSamlContext_NoStoreRegistered_Throws()
    {
        var context = new SamlAuthenticationContext();

        await Should.ThrowAsync<InvalidOperationException>(
            () => _subject.DenyAuthenticationAsync(context, InteractionError.AccessDenied, _ct));
    }

    [Fact]
    public async Task DenyAuthenticationAsync_WithSamlContext_WritesDenialToStateStore()
    {
        var state = new SamlAuthenticationState
        {
            ServiceProviderEntityId = "https://sp.example.com",
            AssertionConsumerService = new IndexedEndpoint { Location = "https://sp.example.com/acs" }
        };
        var spyStore = new SpySamlSigninStateStore(retrieveReturns: state);

        var subject = new DefaultIdentityServerInteractionService(
            _options,
            new FakeTimeProvider(),
            _mockMockHttpContextAccessor,
            _mockLogoutMessageStore,
            _mockErrorMessageStore,
            _mockConsentStore,
            _mockPersistedGrantService,
            _mockUserSession,
            _mockReturnUrlParser,
            TestLogger.Create<DefaultIdentityServerInteractionService>(),
            spyStore
        );

        var stateId = Guid.NewGuid();
        var context = new SamlAuthenticationContext { StateId = stateId };

        await subject.DenyAuthenticationAsync(context, InteractionError.AccessDenied, _ct, "user refused");

        spyStore.UpdateCallCount.ShouldBe(1);
        spyStore.LastUpdatedStateId.ShouldBe(stateId);
        spyStore.LastUpdatedState!.DenialError.ShouldBe(InteractionError.AccessDenied);
        spyStore.LastUpdatedState.DenialErrorDescription.ShouldBe("user refused");
    }

    [Fact]
    public async Task DenyAuthorizationAsync_DelegatesToDenyAuthenticationAsync()
    {
        var request = new AuthorizationRequest
        {
            Client = new Client { ClientId = "client1" },
            ValidatedResources = _resourceValidationResult
        };

        await _subject.DenyAuthorizationAsync(request, InteractionError.ConsentRequired, _ct, "consent denied");

        var stored = _mockConsentStore.Messages.Values.Single();
        stored.Data.Error.ShouldBe(InteractionError.ConsentRequired);
        stored.Data.ErrorDescription.ShouldBe("consent denied");
    }

    [Fact]
    public async Task DenyAuthenticationAsync_WithSamlContext_ExpiredState_IsNoOp()
    {
        // retrieveReturns: null simulates expired/missing state
        var spyStore = new SpySamlSigninStateStore(retrieveReturns: null);

        var subject = new DefaultIdentityServerInteractionService(
            _options,
            new FakeTimeProvider(),
            _mockMockHttpContextAccessor,
            _mockLogoutMessageStore,
            _mockErrorMessageStore,
            _mockConsentStore,
            _mockPersistedGrantService,
            _mockUserSession,
            _mockReturnUrlParser,
            TestLogger.Create<DefaultIdentityServerInteractionService>(),
            spyStore
        );

        var stateId = Guid.NewGuid();
        var context = new SamlAuthenticationContext { StateId = stateId };

        // Should not throw — graceful no-op
        await subject.DenyAuthenticationAsync(context, InteractionError.AccessDenied, _ct);

        spyStore.UpdateCallCount.ShouldBe(0);
    }
}
