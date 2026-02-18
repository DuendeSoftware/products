// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Collections.Specialized;
using System.Security.Claims;
using Duende.IdentityServer;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Saml;
using Duende.IdentityServer.Saml.Models;
using Duende.IdentityServer.Validation;
using UnitTests.Common;

namespace UnitTests.Validation.EndSessionRequestValidation;

public class EndSessionRequestValidatorTests
{
    private EndSessionRequestValidator _subject;
    private IdentityServerOptions _options;
    private readonly Ct _ct = TestContext.Current.CancellationToken;
    private StubTokenValidator _stubTokenValidator = new StubTokenValidator();
    private StubRedirectUriValidator _stubRedirectUriValidator = new StubRedirectUriValidator();
    private MockUserSession _userSession = new MockUserSession();
    private MockLogoutNotificationService _mockLogoutNotificationService = new MockLogoutNotificationService();
    private MockSamlLogoutNotificationService _mockSamlLogoutNotificationService = new MockSamlLogoutNotificationService();
    private MockMessageStore<LogoutNotificationContext> _mockEndSessionMessageStore = new MockMessageStore<LogoutNotificationContext>();

    private ClaimsPrincipal _user;

    public EndSessionRequestValidatorTests()
    {
        _user = new IdentityServerUser("alice").CreatePrincipal();

        _options = TestIdentityServerOptions.Create();
        _subject = new EndSessionRequestValidator(
            _options,
            _stubTokenValidator,
            _stubRedirectUriValidator,
            _userSession,
            _mockLogoutNotificationService,
            _mockSamlLogoutNotificationService,
            _mockEndSessionMessageStore,
            TestLogger.Create<EndSessionRequestValidator>());
    }

    [Fact]
    public async Task anonymous_user_when_options_require_authenticated_user_should_return_error()
    {
        _options.Authentication.RequireAuthenticatedUserForSignOutMessage = true;

        var parameters = new NameValueCollection();
        var result = await _subject.ValidateAsync(parameters, null, _ct);
        result.IsError.ShouldBeTrue();

        result = await _subject.ValidateAsync(parameters, new ClaimsPrincipal(), _ct);
        result.IsError.ShouldBeTrue();

        result = await _subject.ValidateAsync(parameters, new ClaimsPrincipal(new ClaimsIdentity()), _ct);
        result.IsError.ShouldBeTrue();
    }

    [Fact]
    public async Task valid_params_should_return_success()
    {
        _stubTokenValidator.IdentityTokenValidationResult = new TokenValidationResult()
        {
            IsError = false,
            Claims = new Claim[] { new Claim("sub", _user.GetSubjectId()) },
            Client = new Client() { ClientId = "client" }
        };
        _stubRedirectUriValidator.IsPostLogoutRedirectUriValid = true;

        var parameters = new NameValueCollection();
        parameters.Add("id_token_hint", "id_token");
        parameters.Add("post_logout_redirect_uri", "http://client/signout-cb");
        parameters.Add("client_id", "client1");
        parameters.Add("state", "foo");

        var result = await _subject.ValidateAsync(parameters, _user, _ct);
        result.IsError.ShouldBeFalse();

        result.ValidatedRequest.Client.ClientId.ShouldBe("client");
        result.ValidatedRequest.PostLogOutUri.ShouldBe("http://client/signout-cb");
        result.ValidatedRequest.State.ShouldBe("foo");
        result.ValidatedRequest.Subject.GetSubjectId().ShouldBe(_user.GetSubjectId());
    }

    [Fact]
    public async Task no_post_logout_redirect_uri_should_not_use_single_registered_uri()
    {
        _stubTokenValidator.IdentityTokenValidationResult = new TokenValidationResult()
        {
            IsError = false,
            Claims = new Claim[] { new Claim("sub", _user.GetSubjectId()) },
            Client = new Client() { ClientId = "client1", PostLogoutRedirectUris = new List<string> { "foo" } }
        };
        _stubRedirectUriValidator.IsPostLogoutRedirectUriValid = true;

        var parameters = new NameValueCollection();
        parameters.Add("id_token_hint", "id_token");

        var result = await _subject.ValidateAsync(parameters, _user, _ct);
        result.IsError.ShouldBeFalse();
        result.ValidatedRequest.PostLogOutUri.ShouldBeNull();
    }

    [Fact]
    public async Task no_post_logout_redirect_uri_should_not_use_multiple_registered_uri()
    {
        _stubTokenValidator.IdentityTokenValidationResult = new TokenValidationResult()
        {
            IsError = false,
            Claims = new Claim[] { new Claim("sub", _user.GetSubjectId()) },
            Client = new Client() { ClientId = "client1", PostLogoutRedirectUris = new List<string> { "foo", "bar" } }
        };
        _stubRedirectUriValidator.IsPostLogoutRedirectUriValid = true;

        var parameters = new NameValueCollection();
        parameters.Add("id_token_hint", "id_token");

        var result = await _subject.ValidateAsync(parameters, _user, _ct);
        result.IsError.ShouldBeFalse();
        result.ValidatedRequest.PostLogOutUri.ShouldBeNull();
    }

    [Fact]
    public async Task post_logout_uri_fails_validation_should_not_honor_logout_uri()
    {
        _stubTokenValidator.IdentityTokenValidationResult = new TokenValidationResult()
        {
            IsError = false,
            Claims = new Claim[] { new Claim("sub", _user.GetSubjectId()) },
            Client = new Client() { ClientId = "client" }
        };
        _stubRedirectUriValidator.IsPostLogoutRedirectUriValid = false;

        var parameters = new NameValueCollection();
        parameters.Add("id_token_hint", "id_token");
        parameters.Add("post_logout_redirect_uri", "http://client/signout-cb");
        parameters.Add("client_id", "client1");
        parameters.Add("state", "foo");

        var result = await _subject.ValidateAsync(parameters, _user, _ct);
        result.IsError.ShouldBeFalse();

        result.ValidatedRequest.Client.ClientId.ShouldBe("client");
        result.ValidatedRequest.Subject.GetSubjectId().ShouldBe(_user.GetSubjectId());

        result.ValidatedRequest.State.ShouldBeNull();
        result.ValidatedRequest.PostLogOutUri.ShouldBeNull();
    }

    [Fact]
    public async Task subject_mismatch_should_return_error()
    {
        _stubTokenValidator.IdentityTokenValidationResult = new TokenValidationResult()
        {
            IsError = false,
            Claims = new Claim[] { new Claim("sub", "xoxo") },
            Client = new Client() { ClientId = "client" }
        };
        _stubRedirectUriValidator.IsPostLogoutRedirectUriValid = true;

        var parameters = new NameValueCollection();
        parameters.Add("id_token_hint", "id_token");
        parameters.Add("post_logout_redirect_uri", "http://client/signout-cb");
        parameters.Add("client_id", "client1");
        parameters.Add("state", "foo");

        var result = await _subject.ValidateAsync(parameters, _user, _ct);
        result.IsError.ShouldBeTrue();
    }

    [Fact]
    public async Task successful_request_should_return_inputs()
    {
        var parameters = new NameValueCollection();

        var result = await _subject.ValidateAsync(parameters, _user, _ct);
        result.IsError.ShouldBeFalse();
        result.ValidatedRequest.Raw.ShouldBeSameAs(parameters);
    }

    [Fact]
    public async Task successful_request_with_saml_sessions_should_populate_saml_sessions()
    {
        _userSession.User = _user;
        _userSession.SamlSessions =
        [
            new() { EntityId = "https://sp1.example.com", SessionIndex = "idx1", NameId = "user1" },
            new() { EntityId = "https://sp2.example.com", SessionIndex = "idx2", NameId = "user1" }
        ];

        var parameters = new NameValueCollection();

        var result = await _subject.ValidateAsync(parameters, _user);

        result.IsError.ShouldBeFalse();
        result.ValidatedRequest.SamlSessions.ShouldNotBeNull();
        result.ValidatedRequest.SamlSessions.Count().ShouldBe(2);
        result.ValidatedRequest.SamlSessions.Select(s => s.EntityId).ShouldContain("https://sp1.example.com");
        result.ValidatedRequest.SamlSessions.Select(s => s.EntityId).ShouldContain("https://sp2.example.com");
    }

    [Fact]
    public async Task successful_request_without_saml_sessions_should_have_empty_saml_sessions()
    {
        _userSession.User = _user;
        _userSession.SamlSessions = [];

        var parameters = new NameValueCollection();

        var result = await _subject.ValidateAsync(parameters, _user);

        result.IsError.ShouldBeFalse();
        result.ValidatedRequest.SamlSessions.ShouldNotBeNull();
        result.ValidatedRequest.SamlSessions.ShouldBeEmpty();
    }

    [Fact]
    public async Task successful_request_with_both_oidc_and_saml_sessions_should_populate_both()
    {
        _userSession.User = _user;
        _userSession.Clients = ["client1", "client2"];
        _userSession.SamlSessions =
        [
            new() { EntityId = "https://sp1.example.com", SessionIndex = "idx1", NameId = "user1" },
            new() { EntityId = "https://sp2.example.com", SessionIndex = "idx2", NameId = "user1" }
        ];

        var parameters = new NameValueCollection();

        var result = await _subject.ValidateAsync(parameters, _user);

        result.IsError.ShouldBeFalse();

        // OIDC clients
        result.ValidatedRequest.ClientIds.ShouldNotBeNull();
        result.ValidatedRequest.ClientIds.Count().ShouldBe(2);
        result.ValidatedRequest.ClientIds.ShouldContain("client1");
        result.ValidatedRequest.ClientIds.ShouldContain("client2");

        // SAML SPs
        result.ValidatedRequest.SamlSessions.ShouldNotBeNull();
        result.ValidatedRequest.SamlSessions.Count().ShouldBe(2);
        result.ValidatedRequest.SamlSessions.Select(s => s.EntityId).ShouldContain("https://sp1.example.com");
        result.ValidatedRequest.SamlSessions.Select(s => s.EntityId).ShouldContain("https://sp2.example.com");
    }

    [Fact]
    public async Task successful_request_with_id_token_hint_should_collect_saml_sessions()
    {
        _stubTokenValidator.IdentityTokenValidationResult = new TokenValidationResult()
        {
            IsError = false,
            Claims = [new Claim("sub", _user.GetSubjectId())],
            Client = new Client() { ClientId = "client" }
        };
        _userSession.User = _user;
        _userSession.SamlSessions =
        [
            new() { EntityId = "https://sp1.example.com", SessionIndex = "idx1", NameId = "user1" }
        ];

        var parameters = new NameValueCollection();
        parameters.Add("id_token_hint", "id_token");

        var result = await _subject.ValidateAsync(parameters, _user);

        result.IsError.ShouldBeFalse();
        result.ValidatedRequest.SamlSessions.ShouldNotBeNull();
        result.ValidatedRequest.SamlSessions.Select(s => s.EntityId).ShouldContain("https://sp1.example.com");
    }

    [Fact]
    public async Task validate_callback_async_with_only_saml_service_providers_return_success()
    {
        var context = new LogoutNotificationContext
        {
            SubjectId = "test",
            SessionId = "session123",
            ClientIds = [],
            SamlSessions =
            [
                new SamlSpSessionData
                {
                    EntityId = "https://sp1.example.com",
                    NameId = "user@example.com",
                    NameIdFormat = "urn:oasis:names:tc:SAML:1.1:nameid-format:emailAddress",
                    SessionIndex = "session123"
                }
            ]
        };
        _mockEndSessionMessageStore.Messages["endSessionId123"] = new Message<LogoutNotificationContext>(context, DateTime.UtcNow);

        var samlLogout = new MockSamlFrontChannelLogout();
        _mockSamlLogoutNotificationService.SamlFrontChannelLogouts.Add(samlLogout);

        var parameters = new NameValueCollection
        {
            { "endSessionId", "endSessionId123" }
        };

        var result = await _subject.ValidateCallbackAsync(parameters);

        result.IsError.ShouldBeFalse();
        result.SamlFrontChannelLogouts.ShouldNotBeNull();
        result.SamlFrontChannelLogouts.ShouldHaveSingleItem();
        _mockSamlLogoutNotificationService.GetSamlFrontChannelLogoutsAsyncCalled.ShouldBeTrue();
    }

    [Fact]
    public async Task validate_callback_async_with_both_oidc_and_saml_returns_both()
    {
        var context = new LogoutNotificationContext
        {
            SubjectId = "test",
            SessionId = "session123",
            ClientIds = ["client1"],
            SamlSessions = [
                new SamlSpSessionData
                {
                    EntityId = "https://sp1.example.com",
                    NameId = "user@example.com",
                    NameIdFormat = "urn:oasis:names:tc:SAML:1.1:nameid-format:emailAddress",
                    SessionIndex = "session1"
                }
            ]
        };
        _mockEndSessionMessageStore.Messages["endSessionId123"] = new Message<LogoutNotificationContext>(context, DateTime.UtcNow);

        _mockLogoutNotificationService.FrontChannelLogoutNotificationsUrls.Add("http://client1.com/logout");
        var samlLogout = new MockSamlFrontChannelLogout();
        _mockSamlLogoutNotificationService.SamlFrontChannelLogouts.Add(samlLogout);

        var parameters = new NameValueCollection
        {
            { "endSessionId", "endSessionId123" }
        };

        var result = await _subject.ValidateCallbackAsync(parameters);

        result.IsError.ShouldBeFalse();
        result.FrontChannelLogoutUrls.ShouldHaveSingleItem();
        result.SamlFrontChannelLogouts.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task validate_callback_async_with_only_saml_empty_list_returns_error()
    {
        var context = new LogoutNotificationContext
        {
            SubjectId = "test",
            SessionId = "session123",
            ClientIds = [],
            SamlSessions = []
        };
        _mockEndSessionMessageStore.Messages["endSessionId123"] = new Message<LogoutNotificationContext>(context, DateTime.UtcNow);

        var parameters = new NameValueCollection
        {
            { "endSessionId", "endSessionId123" }
        };

        var result = await _subject.ValidateCallbackAsync(parameters);

        result.IsError.ShouldBeTrue();
    }

    [Fact]
    public async Task validate_callback_async_with_saml_passes_context_to_saml_notification_service()
    {
        var context = new LogoutNotificationContext
        {
            SubjectId = "test_user",
            SessionId = "session123",
            ClientIds = [],
            SamlSessions = [
                new SamlSpSessionData
                {
                    EntityId = "https://sp1.example.com",
                    NameId = "user@example.com",
                    NameIdFormat = "urn:oasis:names:tc:SAML:1.1:nameid-format:emailAddress",
                    SessionIndex = "session1"
                },
                new SamlSpSessionData
                {
                    EntityId = "https://sp2.example.com",
                    NameId = "user@example.com",
                    NameIdFormat = "urn:oasis:names:tc:SAML:1.1:nameid-format:emailAddress",
                    SessionIndex = "session2"
                }
            ]
        };
        _mockEndSessionMessageStore.Messages["endSessionId123"] = new Message<LogoutNotificationContext>(context, DateTime.UtcNow);

        _mockSamlLogoutNotificationService.SamlFrontChannelLogouts.Add(new MockSamlFrontChannelLogout());

        var parameters = new NameValueCollection
        {
            { "endSessionId", "endSessionId123" }
        };

        await _subject.ValidateCallbackAsync(parameters);

        _mockSamlLogoutNotificationService.GetSamlFrontChannelLogoutsAsyncCalled.ShouldBeTrue();
    }

    [Fact]
    public async Task validate_callback_async_with_multiple_saml_service_providers_returns_all()
    {
        var context = new LogoutNotificationContext
        {
            SubjectId = "test",
            SessionId = "session123",
            ClientIds = [],
            SamlSessions = [
                new SamlSpSessionData
                {
                    EntityId = "https://sp1.example.com",
                    NameId = "user@example.com",
                    NameIdFormat = "urn:oasis:names:tc:SAML:1.1:nameid-format:emailAddress",
                    SessionIndex = "session1"
                },
                new SamlSpSessionData
                {
                    EntityId = "https://sp2.example.com",
                    NameId = "user@example.com",
                    NameIdFormat = "urn:oasis:names:tc:SAML:1.1:nameid-format:emailAddress",
                    SessionIndex = "session2"
                },
                new SamlSpSessionData
                {
                    EntityId = "https://sp3.example.com",
                    NameId = "user@example.com",
                    NameIdFormat = "urn:oasis:names:tc:SAML:1.1:nameid-format:emailAddress",
                    SessionIndex = "session3"
                }
            ]
        };
        _mockEndSessionMessageStore.Messages["endSessionId123"] = new Message<LogoutNotificationContext>(context, DateTime.UtcNow);

        _mockSamlLogoutNotificationService.SamlFrontChannelLogouts.Add(new MockSamlFrontChannelLogout());
        _mockSamlLogoutNotificationService.SamlFrontChannelLogouts.Add(new MockSamlFrontChannelLogout());
        _mockSamlLogoutNotificationService.SamlFrontChannelLogouts.Add(new MockSamlFrontChannelLogout());

        var parameters = new NameValueCollection
        {
            { "endSessionId", "endSessionId123" }
        };

        var result = await _subject.ValidateCallbackAsync(parameters);

        result.IsError.ShouldBeFalse();
        result.SamlFrontChannelLogouts.Count().ShouldBe(3);
    }

    private class MockSamlFrontChannelLogout : ISamlFrontChannelLogout
    {
        public SamlBinding SamlBinding => SamlBinding.HttpRedirect;
        public Uri Destination => new Uri("https://sp.example.com/slo");
        public string EncodedContent => "encoded";
        public string RelayState => null;
    }
}
