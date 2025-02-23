// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Collections.Generic;
using System.Collections.Specialized;
using System.Security.Claims;
using System.Threading.Tasks;
using Duende.IdentityServer;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Validation;
using Shouldly;
using UnitTests.Common;
using Xunit;

namespace UnitTests.Validation.EndSessionRequestValidation;

public class EndSessionRequestValidatorTests
{
    private EndSessionRequestValidator _subject;
    private IdentityServerOptions _options;
    private StubTokenValidator _stubTokenValidator = new StubTokenValidator();
    private StubRedirectUriValidator _stubRedirectUriValidator = new StubRedirectUriValidator();
    private MockUserSession _userSession = new MockUserSession();
    private MockLogoutNotificationService _mockLogoutNotificationService = new MockLogoutNotificationService();
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
            _mockEndSessionMessageStore,
            TestLogger.Create<EndSessionRequestValidator>());
    }

    [Fact]
    public async Task anonymous_user_when_options_require_authenticated_user_should_return_error()
    {
        _options.Authentication.RequireAuthenticatedUserForSignOutMessage = true;

        var parameters = new NameValueCollection();
        var result = await _subject.ValidateAsync(parameters, null);
        result.IsError.ShouldBeTrue();

        result = await _subject.ValidateAsync(parameters, new ClaimsPrincipal());
        result.IsError.ShouldBeTrue();

        result = await _subject.ValidateAsync(parameters, new ClaimsPrincipal(new ClaimsIdentity()));
        result.IsError.ShouldBeTrue();
    }

    [Fact]
    public async Task valid_params_should_return_success()
    {
        _stubTokenValidator.IdentityTokenValidationResult = new TokenValidationResult()
        {
            IsError = false,
            Claims = new Claim[] { new Claim("sub", _user.GetSubjectId()) },
            Client = new Client() { ClientId = "client"}
        };
        _stubRedirectUriValidator.IsPostLogoutRedirectUriValid = true;

        var parameters = new NameValueCollection();
        parameters.Add("id_token_hint", "id_token");
        parameters.Add("post_logout_redirect_uri", "http://client/signout-cb");
        parameters.Add("client_id", "client1");
        parameters.Add("state", "foo");

        var result = await _subject.ValidateAsync(parameters, _user);
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

        var result = await _subject.ValidateAsync(parameters, _user);
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

        var result = await _subject.ValidateAsync(parameters, _user);
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

        var result = await _subject.ValidateAsync(parameters, _user);
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

        var result = await _subject.ValidateAsync(parameters, _user);
        result.IsError.ShouldBeTrue();
    }

    [Fact]
    public async Task successful_request_should_return_inputs()
    {
        var parameters = new NameValueCollection();

        var result = await _subject.ValidateAsync(parameters, _user);
        result.IsError.ShouldBeFalse();
        result.ValidatedRequest.Raw.ShouldBeSameAs(parameters);
    }
}