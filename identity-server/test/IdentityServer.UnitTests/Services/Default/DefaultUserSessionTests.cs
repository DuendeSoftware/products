// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Net;
using System.Security.Claims;
using Duende.IdentityServer;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using UnitTests.Common;

namespace UnitTests.Services.Default;

public class DefaultUserSessionTests
{
    private DefaultUserSession _subject;
    private MockHttpContextAccessor _mockHttpContext = new MockHttpContextAccessor();
    private MockAuthenticationHandlerProvider _mockAuthenticationHandlerProvider = new MockAuthenticationHandlerProvider();
    private MockAuthenticationHandler _mockAuthenticationHandler = new MockAuthenticationHandler();

    private IdentityServerOptions _options = new IdentityServerOptions();
    private ClaimsPrincipal _user;
    private AuthenticationProperties _props = new AuthenticationProperties();

    public DefaultUserSessionTests()
    {
        _mockAuthenticationHandlerProvider.Handler = _mockAuthenticationHandler;

        _user = new IdentityServerUser("123").CreatePrincipal();
        _subject = new DefaultUserSession(
            _mockHttpContext,
            _mockAuthenticationHandlerProvider,
            _options,
            new StubClock(),
            new MockServerUrls { Origin = "https://server" },
            TestLogger.Create<DefaultUserSession>());
    }

    [Fact]
    public async Task CreateSessionId_when_user_is_anonymous_should_generate_new_sid()
    {
        await _subject.CreateSessionIdAsync(_user, _props);

        _props.GetSessionId().ShouldNotBeNull();
    }

    [Fact]
    public async Task CreateSessionId_should_allow_sid_to_be_indicated_in_properties()
    {
        // this test is needed to allow same session id when cookie is slid
        // IOW, if UI layer passes in same properties dictionary, then we assume it's the same user

        var newProps = new AuthenticationProperties();
        newProps.SetSessionId("999");
        await _subject.CreateSessionIdAsync(_user, newProps);

        newProps.GetSessionId().ShouldNotBeNull();
        newProps.GetSessionId().ShouldBe("999");
    }

    [Fact]
    public async Task CreateSessionId_when_current_props_does_not_contain_key_should_generate_new_sid()
    {
        _mockAuthenticationHandler.Result = AuthenticateResult.Success(new AuthenticationTicket(_user, _props, "scheme"));

        _props.GetSessionId().ShouldBeNull();

        var newProps = new AuthenticationProperties();
        await _subject.CreateSessionIdAsync(_user, newProps);

        newProps.GetSessionId().ShouldNotBeNull();
    }

    [Fact]
    public async Task CreateSessionId_when_user_is_authenticated_but_different_sub_should_create_new_sid()
    {
        _props.SetSessionId("999");
        _mockAuthenticationHandler.Result = AuthenticateResult.Success(new AuthenticationTicket(_user, _props, "scheme"));

        var newProps = new AuthenticationProperties();
        await _subject.CreateSessionIdAsync(new IdentityServerUser("alice").CreatePrincipal(), newProps);

        newProps.GetSessionId().ShouldNotBeNull();
        newProps.GetSessionId().ShouldNotBe("999");
    }

    [Fact]
    public async Task CreateSessionId_when_user_is_authenticated_and_same_sub_should_preserve_sid()
    {
        _props.SetSessionId("999");
        _mockAuthenticationHandler.Result = AuthenticateResult.Success(new AuthenticationTicket(_user, _props, "scheme"));

        var newProps = new AuthenticationProperties();
        await _subject.CreateSessionIdAsync(_user, newProps);

        newProps.GetSessionId().ShouldNotBeNull();
        newProps.GetSessionId().ShouldBe("999");
    }

    [Fact]
    public async Task CreateSessionId_should_issue_session_id_cookie()
    {
        await _subject.CreateSessionIdAsync(_user, _props);

        var cookieContainer = new CookieContainer();
        var cookies = _mockHttpContext.HttpContext.Response.Headers.Where(x => x.Key.Equals("Set-Cookie", StringComparison.OrdinalIgnoreCase)).Select(x => x.Value);
        cookieContainer.SetCookies(new Uri("http://server"), string.Join(',', cookies));
        _mockHttpContext.HttpContext.Response.Headers.Clear();

        var cookie = cookieContainer.GetCookies(new Uri("http://server")).FirstOrDefault(x => x.Name == _options.Authentication.CheckSessionCookieName);
        cookie.Value.ShouldBe(_props.GetSessionId());
    }

    [Fact]
    public async Task EnsureSessionIdCookieAsync_should_add_cookie()
    {
        _props.SetSessionId("999");
        _mockAuthenticationHandler.Result = AuthenticateResult.Success(new AuthenticationTicket(_user, _props, "scheme"));

        await _subject.EnsureSessionIdCookieAsync();

        var cookieContainer = new CookieContainer();
        var cookies = _mockHttpContext.HttpContext.Response.Headers.Where(x => x.Key.Equals("Set-Cookie", StringComparison.OrdinalIgnoreCase)).Select(x => x.Value);
        cookieContainer.SetCookies(new Uri("http://server"), string.Join(',', cookies));
        _mockHttpContext.HttpContext.Response.Headers.Clear();

        var cookie = cookieContainer.GetCookies(new Uri("http://server")).FirstOrDefault(x => x.Name == _options.Authentication.CheckSessionCookieName);
        cookie.Value.ShouldBe("999");
    }

    [Fact]
    public async Task EnsureSessionIdCookieAsync_should_not_add_cookie_if_no_sid()
    {
        await _subject.EnsureSessionIdCookieAsync();

        var cookieContainer = new CookieContainer();
        var cookies = _mockHttpContext.HttpContext.Response.Headers.Where(x => x.Key.Equals("Set-Cookie", StringComparison.OrdinalIgnoreCase)).Select(x => x.Value);
        cookieContainer.SetCookies(new Uri("http://server"), string.Join(',', cookies));
        _mockHttpContext.HttpContext.Response.Headers.Clear();

        var cookie = cookieContainer.GetCookies(new Uri("http://server")).FirstOrDefault(x => x.Name == _options.Authentication.CheckSessionCookieName);
        cookie.ShouldBeNull();
    }

    [Fact]
    public async Task RemoveSessionIdCookie_should_remove_cookie()
    {
        _props.SetSessionId("999");
        _mockAuthenticationHandler.Result = AuthenticateResult.Success(new AuthenticationTicket(_user, _props, "scheme"));

        await _subject.EnsureSessionIdCookieAsync();

        var cookieContainer = new CookieContainer();
        var cookies = _mockHttpContext.HttpContext.Response.Headers.Where(x => x.Key.Equals("Set-Cookie", StringComparison.OrdinalIgnoreCase)).Select(x => x.Value);
        cookieContainer.SetCookies(new Uri("http://server"), string.Join(',', cookies));
        _mockHttpContext.HttpContext.Response.Headers.Clear();

        var cookie = cookieContainer.GetCookieHeader(new Uri("http://server"));
        _mockHttpContext.HttpContext.Request.Headers.Append("Cookie", cookie);

        await _subject.RemoveSessionIdCookieAsync();

        cookies = _mockHttpContext.HttpContext.Response.Headers.Where(x => x.Key.Equals("Set-Cookie", StringComparison.OrdinalIgnoreCase)).Select(x => x.Value);
        cookieContainer.SetCookies(new Uri("http://server"), string.Join(',', cookies));

        var query = cookieContainer.GetCookies(new Uri("http://server")).Cast<Cookie>().Where(x => x.Name == _options.Authentication.CheckSessionCookieName);
        query.Count().ShouldBe(0);
    }

    [Fact]
    public async Task GetCurrentSessionIdAsync_when_user_is_authenticated_should_return_sid()
    {
        _props.SetSessionId("999");
        _mockAuthenticationHandler.Result = AuthenticateResult.Success(new AuthenticationTicket(_user, _props, "scheme"));

        var sid = await _subject.GetSessionIdAsync();
        sid.ShouldBe("999");
    }

    [Fact]
    public async Task GetCurrentSessionIdAsync_when_user_is_anonymous_should_return_null()
    {
        var sid = await _subject.GetSessionIdAsync();
        sid.ShouldBeNull();
    }

    [Fact]
    public async Task adding_client_should_set_item_in_cookie_properties()
    {
        _mockAuthenticationHandler.Result = AuthenticateResult.Success(new AuthenticationTicket(_user, _props, "scheme"));

        _props.Items.Count.ShouldBe(0);
        await _subject.AddClientIdAsync("client");
        _props.Items.Count.ShouldBe(1);
    }

    [Fact]
    public async Task when_handler_successful_GetIdentityServerUserAsync_should_should_return_authenticated_user()
    {
        _mockAuthenticationHandler.Result = AuthenticateResult.Success(new AuthenticationTicket(_user, _props, "scheme"));

        var user = await _subject.GetUserAsync();
        user.GetSubjectId().ShouldBe("123");
    }

    [Fact]
    public async Task when_handler_successful_and_identity_is_anonymous_GetIdentityServerUserAsync_should_should_return_null()
    {
        var cp = new ClaimsPrincipal(new ClaimsIdentity(new Claim[] { new Claim("xoxo", "1") }));
        _mockAuthenticationHandler.Result = AuthenticateResult.Success(new AuthenticationTicket(cp, _props, "scheme"));

        var user = await _subject.GetUserAsync();
        user.ShouldBeNull();
    }

    [Fact]
    public async Task when_anonymous_GetIdentityServerUserAsync_should_return_null()
    {
        var user = await _subject.GetUserAsync();
        user.ShouldBeNull();
    }

    [Fact]
    public async Task corrupt_properties_entry_should_clear_entry()
    {
        _mockAuthenticationHandler.Result = AuthenticateResult.Success(new AuthenticationTicket(_user, _props, "scheme"));

        await _subject.AddClientIdAsync("client");
        var item = _props.Items.First();
        _props.Items[item.Key] = "junk";

        var clients = await _subject.GetClientListAsync();
        clients.ShouldBeEmpty();
        _props.Items.Count.ShouldBe(0);
    }

    [Fact]
    public async Task adding_client_should_be_able_to_read_client()
    {
        _mockAuthenticationHandler.Result = AuthenticateResult.Success(new AuthenticationTicket(_user, _props, "scheme"));

        await _subject.AddClientIdAsync("client");
        var clients = await _subject.GetClientListAsync();
        clients.ShouldBe(["client"]);
    }

    [Fact]
    public async Task adding_clients_should_be_able_to_read_clients()
    {
        _mockAuthenticationHandler.Result = AuthenticateResult.Success(new AuthenticationTicket(_user, _props, "scheme"));

        await _subject.AddClientIdAsync("client1");
        await _subject.AddClientIdAsync("client2");
        var clients = await _subject.GetClientListAsync();
        clients.ShouldBe(["client2", "client1"], true);
    }

    [Fact]
    public async Task adding_existing_client_should_not_add_new_client()
    {
        _mockAuthenticationHandler.Result = AuthenticateResult.Success(new AuthenticationTicket(_user, _props, "scheme"));

        const string clientId = "client";
        await _subject.AddClientIdAsync(clientId);
        await _subject.AddClientIdAsync(clientId);

        var clients = await _subject.GetClientListAsync();

        _props.Items.Count.ShouldBe(1);
        clients.ShouldBe([clientId]);
    }
}
