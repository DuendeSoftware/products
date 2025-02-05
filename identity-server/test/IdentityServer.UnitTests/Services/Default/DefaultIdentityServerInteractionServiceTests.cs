// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System;
using System.Linq;
using System.Threading.Tasks;
using Duende.IdentityServer;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Validation;
using Shouldly;
using UnitTests.Common;
using Xunit;

namespace UnitTests.Services.Default;

public class DefaultIdentityServerInteractionServiceTests
{
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

    private ResourceValidationResult _resourceValidationResult;

    public DefaultIdentityServerInteractionServiceTests()
    {
        _mockMockHttpContextAccessor = new MockHttpContextAccessor(_options, _mockUserSession, _mockEndSessionStore, _mockServerUrls);

        _subject = new DefaultIdentityServerInteractionService(new StubClock(), 
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

        var context = await _subject.GetLogoutContextAsync("id");

        context.SignOutIFrameUrl.ShouldBeNull();
    }

    [Fact]
    public async Task GetLogoutContextAsync_valid_session_no_logout_id_should_provide_iframe()
    {
        _mockUserSession.Clients.Add("foo");
        _mockUserSession.SessionId = "session";
        _mockUserSession.User = new IdentityServerUser("123").CreatePrincipal();

        var context = await _subject.GetLogoutContextAsync(null);

        context.SignOutIFrameUrl.ShouldNotBeNull();
    }

    [Fact]
    public async Task GetLogoutContextAsync_without_session_should_not_provide_iframe()
    {
        _mockUserSession.SessionId = null;
        _mockLogoutMessageStore.Messages.Add("id", new Message<LogoutMessage>(new LogoutMessage()));

        var context = await _subject.GetLogoutContextAsync("id");

        context.SignOutIFrameUrl.ShouldBeNull();
    }

    [Fact]
    public async Task CreateLogoutContextAsync_without_session_should_not_create_session()
    {
        var context = await _subject.CreateLogoutContextAsync();

        context.ShouldBeNull();
        _mockLogoutMessageStore.Messages.ShouldBeEmpty();
    }

    [Fact]
    public async Task CreateLogoutContextAsync_with_session_should_create_session()
    {
        _mockUserSession.Clients.Add("foo");
        _mockUserSession.User = new IdentityServerUser("123").CreatePrincipal();
        _mockUserSession.SessionId = "session";

        var context = await _subject.CreateLogoutContextAsync();

        context.ShouldNotBeNull();
        _mockLogoutMessageStore.Messages.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task GrantConsentAsync_should_throw_if_granted_and_no_subject()
    {
        Func<Task> act = () => _subject.GrantConsentAsync(
            new AuthorizationRequest(), 
            new ConsentResponse() { ScopesValuesConsented = new[] { "openid" } }, 
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
        await _subject.GrantConsentAsync(req, new ConsentResponse { Error = AuthorizationError.AccessDenied }, null);
    }

    [Fact]
    public async Task GrantConsentAsync_should_use_current_subject_and_create_message()
    {
        _mockUserSession.User = new IdentityServerUser("bob").CreatePrincipal();

        var req = new AuthorizationRequest() { 
            Client = new Client { ClientId = "client" },
            ValidatedResources = _resourceValidationResult
        };
        await _subject.GrantConsentAsync(req, new ConsentResponse(), null);

        _mockConsentStore.Messages.ShouldNotBeEmpty();
        var consentRequest = new ConsentRequest(req, "bob");
        _mockConsentStore.Messages.First().Key.ShouldBe(consentRequest.Id);
    }
}