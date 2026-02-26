// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Saml.Models;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Stores;
using Duende.IdentityServer.Validation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
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

        _subject = new DefaultIdentityServerInteractionService(new FakeTimeProvider(),
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
        await _subject.GrantConsentAsync(req, new ConsentResponse { Error = AuthorizationError.AccessDenied }, _ct, null);
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
}
