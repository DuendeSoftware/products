// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Security.Claims;
using Duende.IdentityModel.Client;
using Duende.IdentityServer;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Stores;
using Duende.IdentityServer.Test;
using IntegrationTests.Common;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;

namespace IntegrationTests.Hosting;

public class ServerSideSessionTests
{
    private const string Category = "Server Side Sessions";

    private IdentityServerPipeline _pipeline = new IdentityServerPipeline();
    private IServerSideSessionStore _sessionStore;
    private IServerSideTicketStore _ticketService;
    private ISessionManagementService _sessionMgmt;
    private IPersistedGrantStore _grantStore;
    private IRefreshTokenStore _refreshTokenStore;
    private IDataProtector _protector;

    private MockServerUrls _urls = new MockServerUrls();

    public class MockServerUrls : IServerUrls
    {
        public string Origin { get; set; }
        public string BasePath { get; set; }
    }
    public bool ShouldRenewCookie { get; set; } = false;

    public ServerSideSessionTests()
    {
        _urls.Origin = IdentityServerPipeline.BaseUrl;
        _urls.BasePath = "/";
        _pipeline.OnPostConfigureServices += s =>
        {
            s.PostConfigure<CookieAuthenticationOptions>(IdentityServerConstants.DefaultCookieAuthenticationScheme, opts =>
            {
                opts.Events.OnValidatePrincipal = async ctx =>
                {
                    ctx.ShouldRenew = ShouldRenewCookie;
                    if (ShouldRenewCookie)
                    {
                        await _sessionStore.DeleteSessionsAsync(new SessionFilter { SubjectId = "bob" });
                    }
                };
            });

            s.AddSingleton<IServerUrls>(_urls);
            s.AddIdentityServerBuilder().AddServerSideSessions();
        };
        _pipeline.OnPostConfigure += app =>
        {
            _pipeline.Options.ServerSideSessions.RemoveExpiredSessionsFrequency = TimeSpan.FromMilliseconds(100);

            app.Map("/user", ep =>
            {
                ep.Run(ctx =>
                {
                    if (ctx.User.Identity.IsAuthenticated)
                    {
                        ctx.Response.StatusCode = 200;
                    }
                    else
                    {
                        ctx.Response.StatusCode = 401;
                    }
                    return Task.CompletedTask;
                });
            });
        };


        _pipeline.Users.Add(new TestUser
        {
            SubjectId = "bob",
            Username = "bob",
        });
        _pipeline.Users.Add(new TestUser
        {
            SubjectId = "alice",
            Username = "alice",
        });

        _pipeline.Clients.Add(new Client
        {
            ClientId = "client",
            AllowedGrantTypes = GrantTypes.Code,
            RequireClientSecret = false,
            RequireConsent = false,
            RequirePkce = false,
            AllowedScopes = { "openid", "api" },
            AllowOfflineAccess = true,
            CoordinateLifetimeWithUserSession = true,
            RefreshTokenUsage = TokenUsage.ReUse,
            RedirectUris = { "https://client/callback" },
            BackChannelLogoutUri = "https://client/bc-logout"
        });
        _pipeline.IdentityScopes.Add(new IdentityResources.OpenId());
        _pipeline.ApiScopes.Add(new ApiScope("api"));

        _pipeline.Initialize();

        _sessionStore = _pipeline.Resolve<IServerSideSessionStore>();
        _ticketService = _pipeline.Resolve<IServerSideTicketStore>();
        _sessionMgmt = _pipeline.Resolve<ISessionManagementService>();
        _grantStore = _pipeline.Resolve<IPersistedGrantStore>();
        _refreshTokenStore = _pipeline.Resolve<IRefreshTokenStore>();
        _protector = _pipeline.Resolve<IDataProtectionProvider>().CreateProtector("Duende.SessionManagement.ServerSideTicketStore");
    }

    private async Task<bool> IsLoggedIn()
    {
        var response = await _pipeline.BrowserClient.GetAsync(IdentityServerPipeline.BaseUrl + "/user");
        return response.StatusCode == System.Net.HttpStatusCode.OK;
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task login_should_create_server_side_session()
    {
        (await _sessionStore.GetSessionsAsync(new SessionFilter { SubjectId = "bob" })).ShouldBeEmpty();
        await _pipeline.LoginAsync("bob");
        (await _sessionStore.GetSessionsAsync(new SessionFilter { SubjectId = "bob" })).ShouldNotBeEmpty();
        (await IsLoggedIn()).ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task renewal_should_create_new_record_if_missing()
    {
        await _pipeline.LoginAsync("bob");

        ShouldRenewCookie = true;
        (await IsLoggedIn()).ShouldBeTrue();

        (await _sessionStore.GetSessionsAsync(new SessionFilter { SubjectId = "bob" })).ShouldNotBeEmpty();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task remove_server_side_session_should_logout_user()
    {
        await _pipeline.LoginAsync("bob");

        await _sessionStore.DeleteSessionsAsync(new SessionFilter { SubjectId = "bob" });
        (await _sessionStore.GetSessionsAsync(new SessionFilter { SubjectId = "bob" })).ShouldBeEmpty();

        (await IsLoggedIn()).ShouldBeFalse();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task logout_should_remove_server_side_session()
    {
        await _pipeline.LoginAsync("bob");
        await _pipeline.LogoutAsync();

        (await _sessionStore.GetSessionsAsync(new SessionFilter { SubjectId = "bob" })).ShouldBeEmpty();

        (await IsLoggedIn()).ShouldBeFalse();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task corrupted_server_side_session_should_logout_user()
    {
        await _pipeline.LoginAsync("bob");

        var sessions = await _sessionStore.GetSessionsAsync(new SessionFilter { SubjectId = "bob" });
        var session = await _sessionStore.GetSessionAsync(sessions.Single().Key);
        session.Ticket = "invalid";
        await _sessionStore.UpdateSessionAsync(session);

        (await IsLoggedIn()).ShouldBeFalse();
        (await _sessionStore.GetSessionsAsync(new SessionFilter { SubjectId = "bob" })).ShouldBeEmpty();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task subsequent_logins_should_update_server_side_session()
    {
        await _pipeline.LoginAsync("bob");

        var key = (await _sessionStore.GetSessionsAsync(new SessionFilter { SubjectId = "bob" })).Single().Key;

        await _pipeline.LoginAsync("bob");

        (await IsLoggedIn()).ShouldBeTrue();
        var sessions = await _sessionStore.GetSessionsAsync(new SessionFilter { SubjectId = "bob" });
        sessions.First().Key.ShouldBe(key);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task changing_users_should_create_new_server_side_session()
    {
        await _pipeline.LoginAsync("bob");

        var bob_session = (await _sessionStore.GetSessionsAsync(new SessionFilter { SubjectId = "bob" })).Single();

        await Task.Delay(1000);
        await _pipeline.LoginAsync("alice");

        (await IsLoggedIn()).ShouldBeTrue();
        var alice_session = (await _sessionStore.GetSessionsAsync(new SessionFilter { SubjectId = "alice" })).Single();

        alice_session.Key.ShouldBe(bob_session.Key);
        (alice_session.Created > bob_session.Created).ShouldBeTrue();
        alice_session.SessionId.ShouldNotBe(bob_session.SessionId);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task getsessions_on_ticket_store_should_use_session_store()
    {
        await _pipeline.LoginAsync("alice");
        _pipeline.RemoveLoginCookie();
        await _pipeline.LoginAsync("alice");
        _pipeline.RemoveLoginCookie();
        await _pipeline.LoginAsync("alice");
        _pipeline.RemoveLoginCookie();

        var tickets = await _ticketService.GetSessionsAsync(new SessionFilter { SubjectId = "alice" });
        var sessions = await _sessionStore.GetSessionsAsync(new SessionFilter { SubjectId = "alice" });

        tickets.Select(x => x.SessionId).ShouldBe(sessions.Select(x => x.SessionId));
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task querysessions_on_ticket_store_should_use_session_store()
    {
        await _pipeline.LoginAsync("alice");
        _pipeline.RemoveLoginCookie();
        await _pipeline.LoginAsync("alice");
        _pipeline.RemoveLoginCookie();
        await _pipeline.LoginAsync("bob");
        _pipeline.RemoveLoginCookie();

        var tickets = await _ticketService.QuerySessionsAsync(new SessionQuery { SubjectId = "alice" });
        tickets.TotalCount.ShouldBe(2);
        var sessions = await _sessionStore.QuerySessionsAsync(new SessionQuery { SubjectId = "alice" });
        sessions.TotalCount.ShouldBe(2);

        tickets.ResultsToken.ShouldBe(sessions.ResultsToken);
        tickets.HasPrevResults.ShouldBe(sessions.HasPrevResults);
        tickets.HasNextResults.ShouldBe(sessions.HasNextResults);
        tickets.TotalCount.ShouldBe(sessions.TotalCount);
        tickets.TotalPages.ShouldBe(sessions.TotalPages);
        tickets.CurrentPage.ShouldBe(sessions.CurrentPage);

        tickets.Results.Select(x => x.SessionId).ShouldBe(sessions.Results.Select(x => x.SessionId));
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task querysessions_on_session_mgmt_service_should_use_ticket_store()
    {
        await _pipeline.LoginAsync("alice");
        _pipeline.RemoveLoginCookie();
        await _pipeline.LoginAsync("alice");
        _pipeline.RemoveLoginCookie();
        await _pipeline.LoginAsync("alice");
        _pipeline.RemoveLoginCookie();

        var sessions = await _sessionMgmt.QuerySessionsAsync(new SessionQuery { SubjectId = "alice" });
        var tickets = await _ticketService.QuerySessionsAsync(new SessionQuery { SubjectId = "alice" });

        tickets.ResultsToken.ShouldBe(sessions.ResultsToken);
        tickets.HasPrevResults.ShouldBe(sessions.HasPrevResults);
        tickets.HasNextResults.ShouldBe(sessions.HasNextResults);
        tickets.TotalCount.ShouldBe(sessions.TotalCount);
        tickets.TotalPages.ShouldBe(sessions.TotalPages);
        tickets.CurrentPage.ShouldBe(sessions.CurrentPage);

        tickets.Results.Select(x => x.SessionId).ShouldBe(sessions.Results.Select(x => x.SessionId));
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task remove_sessions_should_delete_refresh_tokens()
    {
        await _pipeline.LoginAsync("alice");

        var authzResponse = await _pipeline.RequestAuthorizationEndpointAsync("client", "code", "openid api offline_access", "https://client/callback");
        var tokenResponse = await _pipeline.BackChannelClient.RequestAuthorizationCodeTokenAsync(new AuthorizationCodeTokenRequest
        {
            Address = IdentityServerPipeline.TokenEndpoint,
            ClientId = "client",
            Code = authzResponse.Code,
            RedirectUri = "https://client/callback"
        });

        (await _grantStore.GetAllAsync(new PersistedGrantFilter { SubjectId = "alice" })).ShouldNotBeEmpty();

        await _sessionMgmt.RemoveSessionsAsync(new RemoveSessionsContext
        {
            SubjectId = "alice",
            RemoveServerSideSession = false,
            RevokeConsents = false,
            RevokeTokens = true,
            SendBackchannelLogoutNotification = false
        });

        (await _grantStore.GetAllAsync(new PersistedGrantFilter { SubjectId = "alice" })).ShouldBeEmpty();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task remove_sessions_with_clientid_filter_should_filter_delete_refresh_tokens()
    {
        await _pipeline.LoginAsync("alice");

        var authzResponse = await _pipeline.RequestAuthorizationEndpointAsync("client", "code", "openid api offline_access", "https://client/callback");
        var tokenResponse = await _pipeline.BackChannelClient.RequestAuthorizationCodeTokenAsync(new AuthorizationCodeTokenRequest
        {
            Address = IdentityServerPipeline.TokenEndpoint,
            ClientId = "client",
            Code = authzResponse.Code,
            RedirectUri = "https://client/callback"
        });

        (await _grantStore.GetAllAsync(new PersistedGrantFilter { SubjectId = "alice" })).ShouldNotBeEmpty();

        await _sessionMgmt.RemoveSessionsAsync(new RemoveSessionsContext
        {
            SubjectId = "alice",
            RemoveServerSideSession = false,
            RevokeConsents = false,
            RevokeTokens = true,
            SendBackchannelLogoutNotification = false,
            ClientIds = new[] { "foo" }
        });

        (await _grantStore.GetAllAsync(new PersistedGrantFilter { SubjectId = "alice" })).ShouldNotBeEmpty();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task remove_sessions_should_invoke_backchannel_logout()
    {
        await _pipeline.LoginAsync("alice");

        var authzResponse = await _pipeline.RequestAuthorizationEndpointAsync("client", "code", "openid api offline_access", "https://client/callback");
        var tokenResponse = await _pipeline.BackChannelClient.RequestAuthorizationCodeTokenAsync(new AuthorizationCodeTokenRequest
        {
            Address = IdentityServerPipeline.TokenEndpoint,
            ClientId = "client",
            Code = authzResponse.Code,
            RedirectUri = "https://client/callback"
        });

        _pipeline.BackChannelMessageHandler.InvokeWasCalled.ShouldBeFalse();

        await _sessionMgmt.RemoveSessionsAsync(new RemoveSessionsContext
        {
            SubjectId = "alice",
            RemoveServerSideSession = false,
            RevokeConsents = false,
            RevokeTokens = false,
            SendBackchannelLogoutNotification = true
        });

        _pipeline.BackChannelMessageHandler.InvokeWasCalled.ShouldBeTrue();
    }


    [Fact]
    [Trait("Category", Category)]
    public async Task remove_sessions_with_clientid_filter_should_filter_backchannel_logout()
    {
        await _pipeline.LoginAsync("alice");

        var authzResponse = await _pipeline.RequestAuthorizationEndpointAsync("client", "code", "openid api offline_access", "https://client/callback");
        var tokenResponse = await _pipeline.BackChannelClient.RequestAuthorizationCodeTokenAsync(new AuthorizationCodeTokenRequest
        {
            Address = IdentityServerPipeline.TokenEndpoint,
            ClientId = "client",
            Code = authzResponse.Code,
            RedirectUri = "https://client/callback"
        });

        _pipeline.BackChannelMessageHandler.InvokeWasCalled.ShouldBeFalse();

        await _sessionMgmt.RemoveSessionsAsync(new RemoveSessionsContext
        {
            SubjectId = "alice",
            RemoveServerSideSession = false,
            RevokeConsents = false,
            RevokeTokens = false,
            SendBackchannelLogoutNotification = true,
            ClientIds = new List<string> { "foo" }
        });

        _pipeline.BackChannelMessageHandler.InvokeWasCalled.ShouldBeFalse();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task remove_sessions_should_remove_server_sessions()
    {
        await _pipeline.LoginAsync("alice");

        var authzResponse = await _pipeline.RequestAuthorizationEndpointAsync("client", "code", "openid api offline_access", "https://client/callback");
        var tokenResponse = await _pipeline.BackChannelClient.RequestAuthorizationCodeTokenAsync(new AuthorizationCodeTokenRequest
        {
            Address = IdentityServerPipeline.TokenEndpoint,
            ClientId = "client",
            Code = authzResponse.Code,
            RedirectUri = "https://client/callback"
        });

        _pipeline.BackChannelMessageHandler.InvokeWasCalled.ShouldBeFalse();

        (await _sessionStore.GetSessionsAsync(new SessionFilter { SubjectId = "alice" })).ShouldNotBeEmpty();

        await _sessionMgmt.RemoveSessionsAsync(new RemoveSessionsContext
        {
            SubjectId = "alice",
            RemoveServerSideSession = true,
            RevokeConsents = false,
            RevokeTokens = false,
            SendBackchannelLogoutNotification = false
        });

        (await _sessionStore.GetSessionsAsync(new SessionFilter { SubjectId = "alice" })).ShouldBeEmpty();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task expired_sessions_should_invoke_backchannel_logout()
    {
        _pipeline.Options.ServerSideSessions.ExpiredSessionsTriggerBackchannelLogout = true;

        await _pipeline.LoginAsync("alice");

        var authzResponse = await _pipeline.RequestAuthorizationEndpointAsync("client", "code", "openid api offline_access", "https://client/callback");
        var tokenResponse = await _pipeline.BackChannelClient.RequestAuthorizationCodeTokenAsync(new AuthorizationCodeTokenRequest
        {
            Address = IdentityServerPipeline.TokenEndpoint,
            ClientId = "client",
            Code = authzResponse.Code,
            RedirectUri = "https://client/callback"
        });

        _pipeline.BackChannelMessageHandler.OnInvoke = async msg =>
        {
            var form = await msg.Content.ReadAsStringAsync();
            var jwt = form.Substring("login_token=".Length + 1);
            var handler = new JsonWebTokenHandler();
            var token = handler.ReadJsonWebToken(jwt);
            token.Issuer.ShouldBe(IdentityServerPipeline.BaseUrl);
            token.GetClaim("sub").Value.ShouldBe("alice");
        };
        _pipeline.BackChannelMessageHandler.InvokeWasCalled.ShouldBeFalse();

        var session = (await _sessionStore.GetSessionsAsync(new SessionFilter { SubjectId = "alice" })).Single();
        session.Expires = System.DateTime.UtcNow.AddMinutes(-1);
        await _sessionStore.UpdateSessionAsync(session);

        await Task.Delay(1000);

        _pipeline.BackChannelMessageHandler.InvokeWasCalled.ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task request_to_identity_server_with_expired_session_which_has_not_been_cleaned_up_should_invoke_backchannel_logout()
    {
        _pipeline.Options.ServerSideSessions.ExpiredSessionsTriggerBackchannelLogout = true;
        //simulate a scenario where a session has expired and a request is made to identity server before the job runs
        _pipeline.Options.ServerSideSessions.RemoveExpiredSessions = false;

        await _pipeline.LoginAsync("alice");

        var authzResponse = await _pipeline.RequestAuthorizationEndpointAsync("client", "code", "openid api offline_access", "https://client/callback");
        var tokenResponse = await _pipeline.BackChannelClient.RequestAuthorizationCodeTokenAsync(new AuthorizationCodeTokenRequest
        {
            Address = IdentityServerPipeline.TokenEndpoint,
            ClientId = "client",
            Code = authzResponse.Code,
            RedirectUri = "https://client/callback"
        });

        _pipeline.BackChannelMessageHandler.OnInvoke = async msg =>
        {
            var form = await msg.Content.ReadAsStringAsync();
            var jwt = form.Substring("login_token=".Length + 1);
            var handler = new JsonWebTokenHandler();
            var token = handler.ReadJsonWebToken(jwt);
            token.Issuer.ShouldBe(IdentityServerPipeline.BaseUrl);
            token.GetClaim("sub").Value.ShouldBe("alice");
        };
        _pipeline.BackChannelMessageHandler.InvokeWasCalled.ShouldBeFalse();

        var session = (await _sessionStore.GetSessionsAsync(new SessionFilter { SubjectId = "alice" })).Single();
        session.Expires = System.DateTime.UtcNow.AddMinutes(-1);
        await _sessionStore.UpdateSessionAsync(session);

        await _pipeline.RequestAuthorizationEndpointAsync("client", "code", "openid api offline_access", "https://client/callback");

        _pipeline.BackChannelMessageHandler.InvokeWasCalled.ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task expired_sessions_should_revoke_refresh_token()
    {
        await _pipeline.LoginAsync("alice");

        var authzResponse = await _pipeline.RequestAuthorizationEndpointAsync("client", "code", "openid api offline_access", "https://client/callback");
        var tokenResponse = await _pipeline.BackChannelClient.RequestAuthorizationCodeTokenAsync(new AuthorizationCodeTokenRequest
        {
            Address = IdentityServerPipeline.TokenEndpoint,
            ClientId = "client",
            Code = authzResponse.Code,
            RedirectUri = "https://client/callback"
        });

        (await _grantStore.GetAllAsync(new PersistedGrantFilter { SubjectId = "alice" })).ShouldNotBeEmpty();

        var session = (await _sessionStore.GetSessionsAsync(new SessionFilter { SubjectId = "alice" })).Single();
        session.Expires = System.DateTime.UtcNow.AddMinutes(-1);
        await _sessionStore.UpdateSessionAsync(session);

        await Task.Delay(1000);

        (await _grantStore.GetAllAsync(new PersistedGrantFilter { SubjectId = "alice" })).ShouldBeEmpty();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task logout_should_revoke_refresh_token()
    {
        await _pipeline.LoginAsync("alice");

        var authzResponse = await _pipeline.RequestAuthorizationEndpointAsync("client", "code", "openid api offline_access", "https://client/callback");
        var tokenResponse = await _pipeline.BackChannelClient.RequestAuthorizationCodeTokenAsync(new AuthorizationCodeTokenRequest
        {
            Address = IdentityServerPipeline.TokenEndpoint,
            ClientId = "client",
            Code = authzResponse.Code,
            RedirectUri = "https://client/callback"
        });

        (await _grantStore.GetAllAsync(new PersistedGrantFilter { SubjectId = "alice" })).ShouldNotBeEmpty();

        await _pipeline.LogoutAsync();

        (await _grantStore.GetAllAsync(new PersistedGrantFilter { SubjectId = "alice" })).ShouldBeEmpty();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task using_refresh_token_should_extend_session()
    {
        await _pipeline.LoginAsync("alice");

        var authzResponse = await _pipeline.RequestAuthorizationEndpointAsync("client", "code", "openid api offline_access", "https://client/callback");
        var tokenResponse = await _pipeline.BackChannelClient.RequestAuthorizationCodeTokenAsync(new AuthorizationCodeTokenRequest
        {
            Address = IdentityServerPipeline.TokenEndpoint,
            ClientId = "client",
            Code = authzResponse.Code,
            RedirectUri = "https://client/callback"
        });

        var ticket1 = (await _sessionStore.GetSessionsAsync(new SessionFilter { SubjectId = "alice" })).Single()
            .Deserialize(_protector, null);
        var expiration1 = ticket1.GetExpiration();
        var issued1 = ticket1.GetIssued();

        await Task.Delay(1000);

        await _pipeline.BackChannelClient.RequestRefreshTokenAsync(new RefreshTokenRequest
        {
            Address = IdentityServerPipeline.TokenEndpoint,
            ClientId = "client",
            RefreshToken = tokenResponse.RefreshToken
        });

        var ticket2 = (await _sessionStore.GetSessionsAsync(new SessionFilter { SubjectId = "alice" })).Single()
            .Deserialize(_protector, null);
        var expiration2 = ticket2.GetExpiration();
        var issued2 = ticket2.GetIssued();

        issued2.ShouldBeGreaterThan(issued1);
        expiration2!.Value.ShouldBeGreaterThan(expiration1.Value);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task using_refresh_token_without_sliding_cookie_expiration_should_not_flag_for_cookie_renewal()
    {
        _pipeline.Options.Authentication.CookieSlidingExpiration = false;

        await _pipeline.LoginAsync("alice", new AuthenticationProperties { IsPersistent = true });

        var authzResponse = await _pipeline.RequestAuthorizationEndpointAsync("client", "code", "openid api offline_access", "https://client/callback");
        var tokenResponse = await _pipeline.BackChannelClient.RequestAuthorizationCodeTokenAsync(new AuthorizationCodeTokenRequest
        {
            Address = IdentityServerPipeline.TokenEndpoint,
            ClientId = "client",
            Code = authzResponse.Code,
            RedirectUri = "https://client/callback"
        });

        await _pipeline.BackChannelClient.RequestRefreshTokenAsync(new RefreshTokenRequest
        {
            Address = IdentityServerPipeline.TokenEndpoint,
            ClientId = "client",
            RefreshToken = tokenResponse.RefreshToken
        });

        var ticket = (await _sessionStore.GetSessionsAsync(new SessionFilter { SubjectId = "alice" })).Single()
            .Deserialize(_protector, null);
        ticket.Properties.Items.ShouldNotContainKey(IdentityServerConstants.ForceCookieRenewalFlag);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task
        using_refresh_token_with_persistent_cookie_which_does_not_allow_renewal_should_not_flag_for_cookie_renewal()
    {
        _pipeline.Options.Authentication.CookieSlidingExpiration = true;

        await _pipeline.LoginAsync("alice", new AuthenticationProperties { IsPersistent = true, AllowRefresh = false });

        var authzResponse = await _pipeline.RequestAuthorizationEndpointAsync("client", "code", "openid api offline_access", "https://client/callback");
        var tokenResponse = await _pipeline.BackChannelClient.RequestAuthorizationCodeTokenAsync(new AuthorizationCodeTokenRequest
        {
            Address = IdentityServerPipeline.TokenEndpoint,
            ClientId = "client",
            Code = authzResponse.Code,
            RedirectUri = "https://client/callback"
        });

        await _pipeline.BackChannelClient.RequestRefreshTokenAsync(new RefreshTokenRequest
        {
            Address = IdentityServerPipeline.TokenEndpoint,
            ClientId = "client",
            RefreshToken = tokenResponse.RefreshToken
        });

        var ticket = (await _sessionStore.GetSessionsAsync(new SessionFilter { SubjectId = "alice" })).Single()
            .Deserialize(_protector, null);
        ticket.Properties.Items.ShouldNotContainKey(IdentityServerConstants.ForceCookieRenewalFlag);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task using_refresh_token_with_non_persistent_cookie_should_not_flag_for_cookie_renewal()
    {
        _pipeline.Options.Authentication.CookieSlidingExpiration = true;

        await _pipeline.LoginAsync("alice", new AuthenticationProperties { IsPersistent = false });

        var authzResponse = await _pipeline.RequestAuthorizationEndpointAsync("client", "code", "openid api offline_access", "https://client/callback");
        var tokenResponse = await _pipeline.BackChannelClient.RequestAuthorizationCodeTokenAsync(new AuthorizationCodeTokenRequest
        {
            Address = IdentityServerPipeline.TokenEndpoint,
            ClientId = "client",
            Code = authzResponse.Code,
            RedirectUri = "https://client/callback"
        });

        await _pipeline.BackChannelClient.RequestRefreshTokenAsync(new RefreshTokenRequest
        {
            Address = IdentityServerPipeline.TokenEndpoint,
            ClientId = "client",
            RefreshToken = tokenResponse.RefreshToken
        });

        var ticket = (await _sessionStore.GetSessionsAsync(new SessionFilter { SubjectId = "alice" })).Single()
            .Deserialize(_protector, null);
        ticket.Properties.Items.ShouldNotContainKey(IdentityServerConstants.ForceCookieRenewalFlag);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task using_refresh_token_with_persistent_cookie_should_flag_cookie_for_renewal()
    {
        _pipeline.Options.Authentication.CookieSlidingExpiration = true;

        await _pipeline.LoginAsync("alice", new AuthenticationProperties { IsPersistent = true });

        var authzResponse = await _pipeline.RequestAuthorizationEndpointAsync("client", "code", "openid api offline_access", "https://client/callback");
        var tokenResponse = await _pipeline.BackChannelClient.RequestAuthorizationCodeTokenAsync(new AuthorizationCodeTokenRequest
        {
            Address = IdentityServerPipeline.TokenEndpoint,
            ClientId = "client",
            Code = authzResponse.Code,
            RedirectUri = "https://client/callback"
        });

        await _pipeline.BackChannelClient.RequestRefreshTokenAsync(new RefreshTokenRequest
        {
            Address = IdentityServerPipeline.TokenEndpoint,
            ClientId = "client",
            RefreshToken = tokenResponse.RefreshToken
        });

        var ticket = (await _sessionStore.GetSessionsAsync(new SessionFilter { SubjectId = "alice" })).Single()
            .Deserialize(_protector, null);
        ticket.Properties.Items.ShouldContainKey(IdentityServerConstants.ForceCookieRenewalFlag);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task using_access_token_should_extend_session()
    {
        await _pipeline.LoginAsync("alice");

        var authzResponse = await _pipeline.RequestAuthorizationEndpointAsync("client", "code", "openid api offline_access", "https://client/callback");
        var tokenResponse = await _pipeline.BackChannelClient.RequestAuthorizationCodeTokenAsync(new AuthorizationCodeTokenRequest
        {
            Address = IdentityServerPipeline.TokenEndpoint,
            ClientId = "client",
            Code = authzResponse.Code,
            RedirectUri = "https://client/callback"
        });

        var expiration1 = (await _sessionStore.GetSessionsAsync(new SessionFilter { SubjectId = "alice" })).Single().Expires.Value;

        await _pipeline.BackChannelClient.GetUserInfoAsync(new UserInfoRequest
        {
            Address = IdentityServerPipeline.UserInfoEndpoint,
            ClientId = "client",
            ClientCredentialStyle = ClientCredentialStyle.PostBody,
            Token = tokenResponse.AccessToken
        });

        var expiration2 = (await _sessionStore.GetSessionsAsync(new SessionFilter { SubjectId = "alice" })).Single().Expires.Value;

        expiration2.ShouldBeGreaterThan(expiration1);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task using_access_token_without_sliding_cookie_expiration_should_not_flag_for_cookie_renewal()
    {
        _pipeline.Options.Authentication.CookieSlidingExpiration = false;

        await _pipeline.LoginAsync("alice", new AuthenticationProperties { IsPersistent = true });

        var authzResponse = await _pipeline.RequestAuthorizationEndpointAsync("client", "code", "openid api offline_access", "https://client/callback");
        var tokenResponse = await _pipeline.BackChannelClient.RequestAuthorizationCodeTokenAsync(new AuthorizationCodeTokenRequest
        {
            Address = IdentityServerPipeline.TokenEndpoint,
            ClientId = "client",
            Code = authzResponse.Code,
            RedirectUri = "https://client/callback"
        });

        await _pipeline.BackChannelClient.GetUserInfoAsync(new UserInfoRequest
        {
            Address = IdentityServerPipeline.UserInfoEndpoint,
            ClientId = "client",
            ClientCredentialStyle = ClientCredentialStyle.PostBody,
            Token = tokenResponse.AccessToken
        });

        var ticket = (await _sessionStore.GetSessionsAsync(new SessionFilter { SubjectId = "alice" })).Single()
            .Deserialize(_protector, null);
        ticket.Properties.Items.ShouldNotContainKey(IdentityServerConstants.ForceCookieRenewalFlag);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task
        using_access_token_with_persistent_cookie_which_does_not_allow_renewal_should_not_flag_for_cookie_renewal()
    {
        _pipeline.Options.Authentication.CookieSlidingExpiration = true;

        await _pipeline.LoginAsync("alice", new AuthenticationProperties { IsPersistent = true, AllowRefresh = false });

        var authzResponse = await _pipeline.RequestAuthorizationEndpointAsync("client", "code", "openid api offline_access", "https://client/callback");
        var tokenResponse = await _pipeline.BackChannelClient.RequestAuthorizationCodeTokenAsync(new AuthorizationCodeTokenRequest
        {
            Address = IdentityServerPipeline.TokenEndpoint,
            ClientId = "client",
            Code = authzResponse.Code,
            RedirectUri = "https://client/callback"
        });

        await _pipeline.BackChannelClient.GetUserInfoAsync(new UserInfoRequest
        {
            Address = IdentityServerPipeline.UserInfoEndpoint,
            ClientId = "client",
            ClientCredentialStyle = ClientCredentialStyle.PostBody,
            Token = tokenResponse.AccessToken
        });

        var ticket = (await _sessionStore.GetSessionsAsync(new SessionFilter { SubjectId = "alice" })).Single()
            .Deserialize(_protector, null);
        ticket.Properties.Items.ShouldNotContainKey(IdentityServerConstants.ForceCookieRenewalFlag);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task using_access_token_with_non_persistent_cookie_should_not_flag_for_cookie_renewal()
    {
        _pipeline.Options.Authentication.CookieSlidingExpiration = true;

        await _pipeline.LoginAsync("alice", new AuthenticationProperties { IsPersistent = false });

        var authzResponse = await _pipeline.RequestAuthorizationEndpointAsync("client", "code", "openid api offline_access", "https://client/callback");
        var tokenResponse = await _pipeline.BackChannelClient.RequestAuthorizationCodeTokenAsync(new AuthorizationCodeTokenRequest
        {
            Address = IdentityServerPipeline.TokenEndpoint,
            ClientId = "client",
            Code = authzResponse.Code,
            RedirectUri = "https://client/callback"
        });

        await _pipeline.BackChannelClient.GetUserInfoAsync(new UserInfoRequest
        {
            Address = IdentityServerPipeline.UserInfoEndpoint,
            ClientId = "client",
            ClientCredentialStyle = ClientCredentialStyle.PostBody,
            Token = tokenResponse.AccessToken
        });

        var ticket = (await _sessionStore.GetSessionsAsync(new SessionFilter { SubjectId = "alice" })).Single()
            .Deserialize(_protector, null);
        ticket.Properties.Items.ShouldNotContainKey(IdentityServerConstants.ForceCookieRenewalFlag);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task using_access_token_with_persistent_cookie_should_flag_cookie_for_renewal()
    {
        _pipeline.Options.Authentication.CookieSlidingExpiration = true;

        await _pipeline.LoginAsync("alice", new AuthenticationProperties { IsPersistent = true });

        var authzResponse = await _pipeline.RequestAuthorizationEndpointAsync("client", "code", "openid api offline_access", "https://client/callback");
        var tokenResponse = await _pipeline.BackChannelClient.RequestAuthorizationCodeTokenAsync(new AuthorizationCodeTokenRequest
        {
            Address = IdentityServerPipeline.TokenEndpoint,
            ClientId = "client",
            Code = authzResponse.Code,
            RedirectUri = "https://client/callback"
        });

        await _pipeline.BackChannelClient.GetUserInfoAsync(new UserInfoRequest
        {
            Address = IdentityServerPipeline.UserInfoEndpoint,
            ClientId = "client",
            ClientCredentialStyle = ClientCredentialStyle.PostBody,
            Token = tokenResponse.AccessToken
        });

        var ticket = (await _sessionStore.GetSessionsAsync(new SessionFilter { SubjectId = "alice" })).Single()
            .Deserialize(_protector, null);
        ticket.Properties.Items.ShouldContainKey(IdentityServerConstants.ForceCookieRenewalFlag);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task using_refresh_token_for_expired_session_should_fail()
    {
        _pipeline.Options.ServerSideSessions.RemoveExpiredSessions = false;

        await _pipeline.LoginAsync("alice");

        var authzResponse = await _pipeline.RequestAuthorizationEndpointAsync("client", "code", "openid api offline_access", "https://client/callback");
        var tokenResponse = await _pipeline.BackChannelClient.RequestAuthorizationCodeTokenAsync(new AuthorizationCodeTokenRequest
        {
            Address = IdentityServerPipeline.TokenEndpoint,
            ClientId = "client",
            Code = authzResponse.Code,
            RedirectUri = "https://client/callback"
        });


        {
            var refreshResponse = await _pipeline.BackChannelClient.RequestRefreshTokenAsync(new RefreshTokenRequest
            {
                Address = IdentityServerPipeline.TokenEndpoint,
                ClientId = "client",
                RefreshToken = tokenResponse.RefreshToken
            });
            refreshResponse.IsError.ShouldBeFalse();
        }


        {
            var session = (await _sessionStore.GetSessionsAsync(new SessionFilter { SubjectId = "alice" })).Single();
            session.Expires = null;
            await _sessionStore.UpdateSessionAsync(session);

            var refreshResponse = await _pipeline.BackChannelClient.RequestRefreshTokenAsync(new RefreshTokenRequest
            {
                Address = IdentityServerPipeline.TokenEndpoint,
                ClientId = "client",
                RefreshToken = tokenResponse.RefreshToken
            });
            refreshResponse.IsError.ShouldBeFalse();
        }


        {
            var session = (await _sessionStore.GetSessionsAsync(new SessionFilter { SubjectId = "alice" })).Single();
            session.Expires = DateTime.UtcNow.AddMinutes(-1);
            await _sessionStore.UpdateSessionAsync(session);

            var refreshResponse = await _pipeline.BackChannelClient.RequestRefreshTokenAsync(new RefreshTokenRequest
            {
                Address = IdentityServerPipeline.TokenEndpoint,
                ClientId = "client",
                RefreshToken = tokenResponse.RefreshToken
            });
            refreshResponse.IsError.ShouldBeTrue();
        }


        {
            await _sessionStore.DeleteSessionsAsync(new SessionFilter { SubjectId = "alice" });

            var refreshResponse = await _pipeline.BackChannelClient.RequestRefreshTokenAsync(new RefreshTokenRequest
            {
                Address = IdentityServerPipeline.TokenEndpoint,
                ClientId = "client",
                RefreshToken = tokenResponse.RefreshToken
            });
            refreshResponse.IsError.ShouldBeTrue();
        }
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task using_access_token_for_expired_session_should_fail()
    {
        _pipeline.Options.ServerSideSessions.RemoveExpiredSessions = false;

        await _pipeline.LoginAsync("alice");

        var authzResponse = await _pipeline.RequestAuthorizationEndpointAsync("client", "code", "openid api offline_access", "https://client/callback");
        var tokenResponse = await _pipeline.BackChannelClient.RequestAuthorizationCodeTokenAsync(new AuthorizationCodeTokenRequest
        {
            Address = IdentityServerPipeline.TokenEndpoint,
            ClientId = "client",
            Code = authzResponse.Code,
            RedirectUri = "https://client/callback"
        });


        {
            var response = await _pipeline.BackChannelClient.GetUserInfoAsync(new UserInfoRequest
            {
                Address = IdentityServerPipeline.UserInfoEndpoint,
                ClientId = "client",
                ClientCredentialStyle = ClientCredentialStyle.PostBody,
                Token = tokenResponse.AccessToken
            });
            response.IsError.ShouldBeFalse();
        }


        {
            var session = (await _sessionStore.GetSessionsAsync(new SessionFilter { SubjectId = "alice" })).Single();
            session.Expires = null;
            await _sessionStore.UpdateSessionAsync(session);

            var response = await _pipeline.BackChannelClient.GetUserInfoAsync(new UserInfoRequest
            {
                Address = IdentityServerPipeline.UserInfoEndpoint,
                ClientId = "client",
                ClientCredentialStyle = ClientCredentialStyle.PostBody,
                Token = tokenResponse.AccessToken
            });
            response.IsError.ShouldBeFalse();
        }


        {
            var session = (await _sessionStore.GetSessionsAsync(new SessionFilter { SubjectId = "alice" })).Single();
            session.Expires = DateTime.UtcNow.AddMinutes(-1);
            await _sessionStore.UpdateSessionAsync(session);

            var response = await _pipeline.BackChannelClient.GetUserInfoAsync(new UserInfoRequest
            {
                Address = IdentityServerPipeline.UserInfoEndpoint,
                ClientId = "client",
                ClientCredentialStyle = ClientCredentialStyle.PostBody,
                Token = tokenResponse.AccessToken
            });
            response.IsError.ShouldBeTrue();
        }


        {
            await _sessionStore.DeleteSessionsAsync(new SessionFilter { SubjectId = "alice" });

            var response = await _pipeline.BackChannelClient.GetUserInfoAsync(new UserInfoRequest
            {
                Address = IdentityServerPipeline.UserInfoEndpoint,
                ClientId = "client",
                ClientCredentialStyle = ClientCredentialStyle.PostBody,
                Token = tokenResponse.AccessToken
            });
            response.IsError.ShouldBeTrue();
        }
    }


    [Fact]
    public async Task claim_issuers_should_be_persisted()
    {
        var claimWithCustomIssuer = new Claim("Test", "true", ClaimValueTypes.Boolean, "Custom Issuer");
        var claimWithDefaultIssuer = new Claim("Test", "false", ClaimValueTypes.Boolean, ClaimsIdentity.DefaultIssuer);

        var user = new IdentityServerUser("alice").CreatePrincipal();
        user.Identities.First().AddClaim(claimWithCustomIssuer);
        user.Identities.First().AddClaim(claimWithDefaultIssuer);

        await _pipeline.LoginAsync(user);

        var ticket = (await _sessionStore.GetSessionsAsync(new SessionFilter { SubjectId = "alice" })).Single()
            .Deserialize(_protector, null);
        var claims = ticket.Principal.Claims;
        claims.ShouldContain(c => c.Issuer == "Custom Issuer" && c.Type == "Test");
        claims.ShouldContain(c => c.Issuer == ClaimsIdentity.DefaultIssuer && c.Type == "Test");
    }
}
