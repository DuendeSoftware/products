// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Net;
using System.Security.Claims;
using Duende.IdentityModel.Client;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.IntegrationTests.Common;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Stores;
using Duende.IdentityServer.Test;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Duende.IdentityServer.IntegrationTests.Hosting;

public abstract class ServerSideSessionTestsBase
{
    private const string Category = "Server Side Sessions";
    private const string ClaimAddedByBrowser = "claim_added_by_browser";

    protected IdentityServerPipeline _pipeline = new IdentityServerPipeline();
    protected IServerSideSessionStore _sessionStore;
    protected IServerSideTicketStore _ticketService;
    private ISessionManagementService _sessionMgmt;
    private IPersistedGrantStore _grantStore;
    private IRefreshTokenStore _refreshTokenStore;
    private IDataProtector _protector;
    private readonly Ct _ct = TestContext.Current.CancellationToken;
    private readonly SessionReadHold _sessionReadHold = new(TestContext.Current.CancellationToken);

    private MockServerUrls _urls = new MockServerUrls();

    public class MockServerUrls : IServerUrls
    {
        public string Origin { get; set; }
        public string BasePath { get; set; }
    }
    public bool ShouldRenewCookie { get; set; } = false;
    private Func<CookieValidatePrincipalContext, Task> _validatePrincipalHook;

    protected ServerSideSessionTestsBase()
    {
        _urls.Origin = IdentityServerPipeline.BaseUrl;
        _urls.BasePath = "/";
        _pipeline.OnPostConfigureServices += s =>
        {
            s.PostConfigure<CookieAuthenticationOptions>(IdentityServerConstants.DefaultCookieAuthenticationScheme, opts =>
            {
                opts.Events.OnValidatePrincipal = async ctx =>
                {
                    ctx.ShouldRenew = ctx.ShouldRenew || ShouldRenewCookie;
                    if (_validatePrincipalHook != null)
                    {
                        await _validatePrincipalHook(ctx);
                    }

                    if (ShouldRenewCookie)
                    {
                        await _sessionStore.DeleteSessionsAsync(new SessionFilter { SubjectId = "bob" }, _ct);
                    }
                };
            });

            s.AddSingleton<IServerUrls>(_urls);
            s.AddIdentityServerBuilder().AddServerSideSessions();

            s.PostConfigure<IdentityServerOptions>(opts =>
            {
                opts.ServerSideSessions.RemoveExpiredSessionsFrequency = TimeSpan.FromMilliseconds(100);
            });
        };
        _pipeline.OnPostConfigure += app =>
        {
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
            app.Map("/signout-then-signin-alice", ep =>
            {
                ep.Run(async ctx =>
                {
                    await ctx.SignOutAsync(IdentityServerConstants.DefaultCookieAuthenticationScheme);
                    await ctx.SignInAsync(IdentityServerConstants.DefaultCookieAuthenticationScheme, new IdentityServerUser("alice").CreatePrincipal());
                });
            });
            app.Map("/add-claim-added-by-browser", ep =>
            {
                ep.Run(async ctx =>
                {
                    var signedInUser = await ctx.AuthenticateAsync(IdentityServerConstants.DefaultCookieAuthenticationScheme);
                    ((ClaimsIdentity)signedInUser.Principal.Identity).AddClaim(new Claim(ClaimAddedByBrowser, "true"));
                    await ctx.SignInAsync(IdentityServerConstants.DefaultCookieAuthenticationScheme, signedInUser.Principal, signedInUser.Properties);
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
        _pipeline.Clients.Add(new Client
        {
            ClientId = "client_opt_out",
            AllowedGrantTypes = GrantTypes.Code,
            RequireClientSecret = false,
            RequireConsent = false,
            RequirePkce = false,
            AllowedScopes = { "openid", "api" },
            AllowOfflineAccess = true,
            CoordinateLifetimeWithUserSession = false,
            RefreshTokenUsage = TokenUsage.ReUse,
            RedirectUris = { "https://client_opt_out/callback" }
        });
        _pipeline.Clients.Add(new Client
        {
            ClientId = "client_inherit",
            AllowedGrantTypes = GrantTypes.Code,
            RequireClientSecret = false,
            RequireConsent = false,
            RequirePkce = false,
            AllowedScopes = { "openid", "api" },
            AllowOfflineAccess = true,
            CoordinateLifetimeWithUserSession = null,
            RefreshTokenUsage = TokenUsage.ReUse,
            RedirectUris = { "https://client_inherit/callback" }
        });
        _pipeline.IdentityScopes.Add(new IdentityResources.OpenId());
        _pipeline.ApiScopes.Add(new ApiScope("api"));
    }

    protected void InitializePipeline()
    {
        _pipeline.OnPostConfigureServices += services =>
        {
            var registeredSessionStore = services.Last(descriptor => descriptor.ServiceType == typeof(IServerSideSessionStore));
            services.Remove(registeredSessionStore);
            services.Add(new ServiceDescriptor(
                typeof(IServerSideSessionStore),
                serviceProvider => new SessionStoreThatCanHoldARead(CreateRegisteredSessionStore(registeredSessionStore, serviceProvider), _sessionReadHold),
                registeredSessionStore.Lifetime));
        };

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
        (await _sessionStore.GetSessionsAsync(new SessionFilter { SubjectId = "bob" }, _ct)).ShouldBeEmpty();
        await _pipeline.LoginAsync("bob");
        (await _sessionStore.GetSessionsAsync(new SessionFilter { SubjectId = "bob" }, _ct)).ShouldNotBeEmpty();
        (await IsLoggedIn()).ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task renewal_should_not_bring_back_a_removed_session()
    {
        await _pipeline.LoginAsync("bob");

        ShouldRenewCookie = true;
        await IsLoggedIn();

        (await IsLoggedIn()).ShouldBeFalse("a renew must not recreate the session that was removed during the request");
        (await _sessionStore.GetSessionsAsync(new SessionFilter { SubjectId = "bob" }, _ct)).ShouldBeEmpty();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task remove_server_side_session_should_logout_user()
    {
        await _pipeline.LoginAsync("bob");

        await _sessionStore.DeleteSessionsAsync(new SessionFilter { SubjectId = "bob" }, _ct);
        (await _sessionStore.GetSessionsAsync(new SessionFilter { SubjectId = "bob" }, _ct)).ShouldBeEmpty();

        (await IsLoggedIn()).ShouldBeFalse();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task logout_should_remove_server_side_session()
    {
        await _pipeline.LoginAsync("bob");
        await _pipeline.LogoutAsync();

        (await _sessionStore.GetSessionsAsync(new SessionFilter { SubjectId = "bob" }, _ct)).ShouldBeEmpty();

        (await IsLoggedIn()).ShouldBeFalse();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task corrupted_server_side_session_should_logout_user()
    {
        await _pipeline.LoginAsync("bob");

        var sessions = await _sessionStore.GetSessionsAsync(new SessionFilter { SubjectId = "bob" }, _ct);
        var session = await _sessionStore.GetSessionAsync(sessions.Single().Key, _ct);
        session.Ticket = "invalid";
        await _sessionStore.UpdateSessionAsync(session, _ct);

        (await IsLoggedIn()).ShouldBeFalse();
        (await _sessionStore.GetSessionsAsync(new SessionFilter { SubjectId = "bob" }, _ct)).ShouldBeEmpty();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task subsequent_logins_should_update_server_side_session()
    {
        await _pipeline.LoginAsync("bob");

        var key = (await _sessionStore.GetSessionsAsync(new SessionFilter { SubjectId = "bob" }, _ct)).Single().Key;

        await _pipeline.LoginAsync("bob");

        (await IsLoggedIn()).ShouldBeTrue();
        var sessions = await _sessionStore.GetSessionsAsync(new SessionFilter { SubjectId = "bob" }, _ct);
        sessions.First().Key.ShouldBe(key);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task changing_users_should_create_new_server_side_session()
    {
        await _pipeline.LoginAsync("bob");

        var bob_session = (await _sessionStore.GetSessionsAsync(new SessionFilter { SubjectId = "bob" }, _ct)).Single();

        await _pipeline.LoginAsync("alice");
        var signedInAfterFirstSignIn = await IsLoggedIn();
        await _pipeline.LoginAsync("alice");

        signedInAfterFirstSignIn.ShouldBeFalse("the first sign-in must not put alice's session under bob's key");
        (await IsLoggedIn()).ShouldBeTrue();
        var alice_session = (await _sessionStore.GetSessionsAsync(new SessionFilter { SubjectId = "alice" }, _ct)).Single();

        alice_session.Key.ShouldNotBe(bob_session.Key);
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

        var tickets = await _ticketService.GetSessionsAsync(new SessionFilter { SubjectId = "alice" }, _ct);
        var sessions = await _sessionStore.GetSessionsAsync(new SessionFilter { SubjectId = "alice" }, _ct);

        tickets.Select(x => x.SessionId).ShouldBe(sessions.Select(x => x.SessionId));
    }

    [Fact]
    [Trait("Category", Category)]
    public virtual async Task querysessions_on_ticket_store_should_use_session_store()
    {
        await _pipeline.LoginAsync("alice");
        _pipeline.RemoveLoginCookie();
        await _pipeline.LoginAsync("alice");
        _pipeline.RemoveLoginCookie();
        await _pipeline.LoginAsync("bob");
        _pipeline.RemoveLoginCookie();

        var tickets = await _ticketService.QuerySessionsAsync(new SessionQuery { SubjectId = "alice" }, _ct);
        tickets.TotalCount.ShouldBe(2);
        var sessions = await _sessionStore.QuerySessionsAsync(_ct, new SessionQuery { SubjectId = "alice" });
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

        var sessions = await _sessionMgmt.QuerySessionsAsync(new SessionQuery { SubjectId = "alice" }, _ct);
        var tickets = await _ticketService.QuerySessionsAsync(new SessionQuery { SubjectId = "alice" }, _ct);

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

        (await _grantStore.GetAllAsync(new PersistedGrantFilter { SubjectId = "alice" }, _ct)).ShouldNotBeEmpty();

        await _sessionMgmt.RemoveSessionsAsync(new RemoveSessionsContext
        {
            SubjectId = "alice",
            RemoveServerSideSession = false,
            RevokeConsents = false,
            RevokeTokens = true,
            SendBackchannelLogoutNotification = false
        }, _ct);

        (await _grantStore.GetAllAsync(new PersistedGrantFilter { SubjectId = "alice" }, _ct)).ShouldBeEmpty();
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

        (await _grantStore.GetAllAsync(new PersistedGrantFilter { SubjectId = "alice" }, _ct)).ShouldNotBeEmpty();

        await _sessionMgmt.RemoveSessionsAsync(new RemoveSessionsContext
        {
            SubjectId = "alice",
            RemoveServerSideSession = false,
            RevokeConsents = false,
            RevokeTokens = true,
            SendBackchannelLogoutNotification = false,
            ClientIds = new[] { "foo" }
        }, _ct);

        (await _grantStore.GetAllAsync(new PersistedGrantFilter { SubjectId = "alice" }, _ct)).ShouldNotBeEmpty();
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
        }, _ct);

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
        }, _ct);

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

        (await _sessionStore.GetSessionsAsync(new SessionFilter { SubjectId = "alice" }, _ct)).ShouldNotBeEmpty();

        await _sessionMgmt.RemoveSessionsAsync(new RemoveSessionsContext
        {
            SubjectId = "alice",
            RemoveServerSideSession = true,
            RevokeConsents = false,
            RevokeTokens = false,
            SendBackchannelLogoutNotification = false
        }, _ct);

        (await _sessionStore.GetSessionsAsync(new SessionFilter { SubjectId = "alice" }, _ct)).ShouldBeEmpty();
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

        var session = (await _sessionStore.GetSessionsAsync(new SessionFilter { SubjectId = "alice" }, _ct)).Single();
        session.Expires = System.DateTime.UtcNow.AddMinutes(-1);
        await _sessionStore.UpdateSessionAsync(session, _ct);

        await Task.Delay(1000);

        await ShouldlyExtensions.ShouldSatisfyEventually(() =>
        {
            _pipeline.BackChannelMessageHandler.InvokeWasCalled.ShouldBeTrue();
            return Task.CompletedTask;
        });
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

        var session = (await _sessionStore.GetSessionsAsync(new SessionFilter { SubjectId = "alice" }, _ct)).Single();
        session.Expires = System.DateTime.UtcNow.AddMinutes(-1);
        await _sessionStore.UpdateSessionAsync(session, _ct);

        await _pipeline.RequestAuthorizationEndpointAsync("client", "code", "openid api offline_access", "https://client/callback");

        _pipeline.BackChannelMessageHandler.InvokeWasCalled.ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task explicit_logout_should_send_exactly_one_backchannel_logout_notification()
    {
        _pipeline.Options.ServerSideSessions.ExpiredSessionsTriggerBackchannelLogout = true;

        await _pipeline.LoginAsync("alice");

        var authzResponse = await _pipeline.RequestAuthorizationEndpointAsync("client", "code", "openid api offline_access", "https://client/callback");
        await _pipeline.BackChannelClient.RequestAuthorizationCodeTokenAsync(new AuthorizationCodeTokenRequest
        {
            Address = IdentityServerPipeline.TokenEndpoint,
            ClientId = "client",
            Code = authzResponse.Code,
            RedirectUri = "https://client/callback"
        });

        _pipeline.BackChannelMessageHandler.InvokeCount.ShouldBe(0);

        _pipeline.BackChannelMessageHandler.OnInvoke = async msg =>
        {
            var form = await msg.Content.ReadAsStringAsync();
            var parsed = System.Web.HttpUtility.ParseQueryString(form);
            var jwt = parsed["logout_token"];
            jwt.ShouldNotBeNullOrEmpty();
            var handler = new JsonWebTokenHandler();
            var token = handler.ReadJsonWebToken(jwt);
            token.Issuer.ShouldBe(IdentityServerPipeline.BaseUrl);
            token.GetClaim("sub").Value.ShouldBe("alice");
        };

        await _pipeline.LogoutAsync();

        _pipeline.BackChannelMessageHandler.InvokeCount.ShouldBe(1);
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

        (await _grantStore.GetAllAsync(new PersistedGrantFilter { SubjectId = "alice" }, _ct)).ShouldNotBeEmpty();

        var session = (await _sessionStore.GetSessionsAsync(new SessionFilter { SubjectId = "alice" }, _ct)).Single();
        session.Expires = System.DateTime.UtcNow.AddMinutes(-1);
        await _sessionStore.UpdateSessionAsync(session, _ct);

        await ShouldlyExtensions.ShouldSatisfyEventually(async () =>
        {
            (await _grantStore.GetAllAsync(new PersistedGrantFilter { SubjectId = "alice" }, _ct)).ShouldBeEmpty();
        });
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

        (await _grantStore.GetAllAsync(new PersistedGrantFilter { SubjectId = "alice" }, _ct)).ShouldNotBeEmpty();

        await _pipeline.LogoutAsync();

        (await _grantStore.GetAllAsync(new PersistedGrantFilter { SubjectId = "alice" }, _ct)).ShouldBeEmpty();
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

        var ticket1 = (await _sessionStore.GetSessionsAsync(new SessionFilter { SubjectId = "alice" }, _ct)).Single()
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

        var ticket2 = (await _sessionStore.GetSessionsAsync(new SessionFilter { SubjectId = "alice" }, _ct)).Single()
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

        var ticket = (await _sessionStore.GetSessionsAsync(new SessionFilter { SubjectId = "alice" }, _ct)).Single()
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

        var ticket = (await _sessionStore.GetSessionsAsync(new SessionFilter { SubjectId = "alice" }, _ct)).Single()
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

        var ticket = (await _sessionStore.GetSessionsAsync(new SessionFilter { SubjectId = "alice" }, _ct)).Single()
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

        var ticket = (await _sessionStore.GetSessionsAsync(new SessionFilter { SubjectId = "alice" }, _ct)).Single()
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

        var expiration1 = (await _sessionStore.GetSessionsAsync(new SessionFilter { SubjectId = "alice" }, _ct)).Single().Expires.Value;

        await _pipeline.BackChannelClient.GetUserInfoAsync(new UserInfoRequest
        {
            Address = IdentityServerPipeline.UserInfoEndpoint,
            ClientId = "client",
            ClientCredentialStyle = ClientCredentialStyle.PostBody,
            Token = tokenResponse.AccessToken
        });

        var expiration2 = (await _sessionStore.GetSessionsAsync(new SessionFilter { SubjectId = "alice" }, _ct)).Single().Expires.Value;

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

        var ticket = (await _sessionStore.GetSessionsAsync(new SessionFilter { SubjectId = "alice" }, _ct)).Single()
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

        var ticket = (await _sessionStore.GetSessionsAsync(new SessionFilter { SubjectId = "alice" }, _ct)).Single()
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

        var ticket = (await _sessionStore.GetSessionsAsync(new SessionFilter { SubjectId = "alice" }, _ct)).Single()
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

        var ticket = (await _sessionStore.GetSessionsAsync(new SessionFilter { SubjectId = "alice" }, _ct)).Single()
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
            var session = (await _sessionStore.GetSessionsAsync(new SessionFilter { SubjectId = "alice" }, _ct)).Single();
            session.Expires = null;
            await _sessionStore.UpdateSessionAsync(session, _ct);

            var refreshResponse = await _pipeline.BackChannelClient.RequestRefreshTokenAsync(new RefreshTokenRequest
            {
                Address = IdentityServerPipeline.TokenEndpoint,
                ClientId = "client",
                RefreshToken = tokenResponse.RefreshToken
            });
            refreshResponse.IsError.ShouldBeFalse();
        }


        {
            var session = (await _sessionStore.GetSessionsAsync(new SessionFilter { SubjectId = "alice" }, _ct)).Single();
            session.Expires = DateTime.UtcNow.AddMinutes(-1);
            await _sessionStore.UpdateSessionAsync(session, _ct);

            var refreshResponse = await _pipeline.BackChannelClient.RequestRefreshTokenAsync(new RefreshTokenRequest
            {
                Address = IdentityServerPipeline.TokenEndpoint,
                ClientId = "client",
                RefreshToken = tokenResponse.RefreshToken
            });
            refreshResponse.IsError.ShouldBeTrue();
        }


        {
            await _sessionStore.DeleteSessionsAsync(new SessionFilter { SubjectId = "alice" }, _ct);

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
            var session = (await _sessionStore.GetSessionsAsync(new SessionFilter { SubjectId = "alice" }, _ct)).Single();
            session.Expires = null;
            await _sessionStore.UpdateSessionAsync(session, _ct);

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
            var session = (await _sessionStore.GetSessionsAsync(new SessionFilter { SubjectId = "alice" }, _ct)).Single();
            session.Expires = DateTime.UtcNow.AddMinutes(-1);
            await _sessionStore.UpdateSessionAsync(session, _ct);

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
            await _sessionStore.DeleteSessionsAsync(new SessionFilter { SubjectId = "alice" }, _ct);

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
    [Trait("Category", Category)]
    public async Task changing_users_should_revoke_prior_sessions_grants()
    {
        // login as bob and get a refresh token
        await _pipeline.LoginAsync("bob");

        var authzResponse = await _pipeline.RequestAuthorizationEndpointAsync("client", "code", "openid api offline_access", "https://client/callback");
        await _pipeline.BackChannelClient.RequestAuthorizationCodeTokenAsync(new AuthorizationCodeTokenRequest
        {
            Address = IdentityServerPipeline.TokenEndpoint,
            ClientId = "client",
            Code = authzResponse.Code,
            RedirectUri = "https://client/callback"
        });

        // verify bob has grants
        (await _grantStore.GetAllAsync(new PersistedGrantFilter { SubjectId = "bob" }, _ct)).ShouldNotBeEmpty();

        // login as alice — overwrites the same session key with a new SessionId
        await _pipeline.LoginAsync("alice");

        // bob's grants should have been cleaned up during the session overwrite
        (await _grantStore.GetAllAsync(new PersistedGrantFilter { SubjectId = "bob" }, _ct)).ShouldBeEmpty();
    }

    private async Task GetRefreshTokenAsync(string clientId)
    {
        var redirectUri = $"https://{clientId}/callback";
        var authzResponse = await _pipeline.RequestAuthorizationEndpointAsync(clientId, "code", "openid api offline_access", redirectUri);
        var tokenResponse = await _pipeline.BackChannelClient.RequestAuthorizationCodeTokenAsync(new AuthorizationCodeTokenRequest
        {
            Address = IdentityServerPipeline.TokenEndpoint,
            ClientId = clientId,
            Code = authzResponse.Code,
            RedirectUri = redirectUri
        });
        tokenResponse.IsError.ShouldBeFalse();
        tokenResponse.RefreshToken.ShouldNotBeNull();
    }

    private async Task<IReadOnlyCollection<PersistedGrant>> GetRefreshTokenGrantsAsync(string subjectId, string clientId) =>
        await _grantStore.GetAllAsync(new PersistedGrantFilter
        {
            SubjectId = subjectId,
            ClientId = clientId,
            Type = IdentityServerConstants.PersistedGrantTypes.RefreshToken
        }, _ct);

    [Fact]
    [Trait("Category", Category)]
    public async Task changing_users_should_not_revoke_prior_sessions_grants_for_clients_that_opt_out_of_coordination()
    {
        await _pipeline.LoginAsync("bob");
        await GetRefreshTokenAsync("client_opt_out");
        (await GetRefreshTokenGrantsAsync("bob", "client_opt_out")).ShouldNotBeEmpty();

        await _pipeline.LoginAsync("alice");

        (await GetRefreshTokenGrantsAsync("bob", "client_opt_out")).ShouldNotBeEmpty();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task changing_users_should_not_revoke_grants_for_clients_that_opt_out_even_when_coordination_is_enabled_globally()
    {
        // An explicit client-level opt-out wins over the global setting.
        _pipeline.Options.Authentication.CoordinateClientLifetimesWithUserSession = true;

        await _pipeline.LoginAsync("bob");
        await GetRefreshTokenAsync("client_opt_out");
        (await GetRefreshTokenGrantsAsync("bob", "client_opt_out")).ShouldNotBeEmpty();

        await _pipeline.LoginAsync("alice");

        (await GetRefreshTokenGrantsAsync("bob", "client_opt_out")).ShouldNotBeEmpty();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task same_user_signing_in_again_with_active_session_should_keep_session_id_and_grants()
    {
        // DefaultUserSession reuses the current sid when the same subject signs in again,
        // so this is not a session replacement and nothing should be revoked.
        await _pipeline.LoginAsync("bob");
        await GetRefreshTokenAsync("client");
        var sessionBefore = (await _sessionStore.GetSessionsAsync(new SessionFilter { SubjectId = "bob" }, _ct)).Single();

        await _pipeline.LoginAsync("bob");

        var sessionAfter = (await _sessionStore.GetSessionsAsync(new SessionFilter { SubjectId = "bob" }, _ct)).Single();
        sessionAfter.Key.ShouldBe(sessionBefore.Key);
        sessionAfter.SessionId.ShouldBe(sessionBefore.SessionId);
        (await GetRefreshTokenGrantsAsync("bob", "client")).ShouldNotBeEmpty();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task session_replacement_should_not_revoke_grants_for_clients_inheriting_disabled_global_coordination()
    {
        _pipeline.Options.Authentication.CoordinateClientLifetimesWithUserSession = false;

        await _pipeline.LoginAsync("bob");
        await GetRefreshTokenAsync("client_inherit");

        await _pipeline.LoginAsync("alice");

        (await GetRefreshTokenGrantsAsync("bob", "client_inherit")).ShouldNotBeEmpty();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task session_replacement_should_revoke_grants_for_clients_inheriting_enabled_global_coordination()
    {
        _pipeline.Options.Authentication.CoordinateClientLifetimesWithUserSession = true;

        await _pipeline.LoginAsync("bob");
        await GetRefreshTokenAsync("client_inherit");
        (await GetRefreshTokenGrantsAsync("bob", "client_inherit")).ShouldNotBeEmpty();

        await _pipeline.LoginAsync("alice");

        (await GetRefreshTokenGrantsAsync("bob", "client_inherit")).ShouldBeEmpty();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task session_replacement_should_only_revoke_grants_for_coordinated_clients()
    {
        await _pipeline.LoginAsync("bob");
        await GetRefreshTokenAsync("client");
        await GetRefreshTokenAsync("client_opt_out");
        (await GetRefreshTokenGrantsAsync("bob", "client")).ShouldNotBeEmpty();
        (await GetRefreshTokenGrantsAsync("bob", "client_opt_out")).ShouldNotBeEmpty();

        await _pipeline.LoginAsync("alice");

        (await GetRefreshTokenGrantsAsync("bob", "client")).ShouldBeEmpty();
        (await GetRefreshTokenGrantsAsync("bob", "client_opt_out")).ShouldNotBeEmpty();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task session_replacement_with_unreadable_previous_ticket_should_still_revoke_grants_for_coordinated_clients()
    {
        await _pipeline.LoginAsync("bob");
        await GetRefreshTokenAsync("client");
        await GetRefreshTokenAsync("client_opt_out");
        (await GetRefreshTokenGrantsAsync("bob", "client")).ShouldNotBeEmpty();
        (await GetRefreshTokenGrantsAsync("bob", "client_opt_out")).ShouldNotBeEmpty();

        // simulate the data protection keys for bob's previous session's ticket being lost
        var session = (await _sessionStore.GetSessionsAsync(new SessionFilter { SubjectId = "bob" }, _ct)).Single();
        session.Ticket = "invalid";
        await _sessionStore.UpdateSessionAsync(session, _ct);

        // session replacement: alice replaces bob's session on the same key. This bypasses the
        // browser/cookie pipeline (which would refuse to renew a session whose ticket it cannot
        // read, and would instead start a brand new session) so that we can exercise
        // ServerSideTicketStore.RenewAsync directly, the way it behaves when the previous
        // ticket cannot be deserialized.
        var newPrincipal = new IdentityServerUser("alice").CreatePrincipal();
        var newProperties = new AuthenticationProperties
        {
            IssuedUtc = DateTimeOffset.UtcNow,
            ExpiresUtc = DateTimeOffset.UtcNow.AddHours(1)
        };
        newProperties.SetSessionId("alice-sid-after-replacement");
        await _ticketService.RenewAsync(session.Key, new AuthenticationTicket(newPrincipal, newProperties, IdentityServerConstants.DefaultCookieAuthenticationScheme));

        (await GetRefreshTokenGrantsAsync("bob", "client")).ShouldBeEmpty();
        (await GetRefreshTokenGrantsAsync("bob", "client_opt_out")).ShouldNotBeEmpty();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task changing_users_should_not_let_a_copy_of_the_previous_users_cookie_reach_the_new_users_session()
    {
        await _pipeline.LoginAsync("bob");
        var bobsCookieCopy = CopyOfSessionCookie();

        await _pipeline.LoginAsync("alice");

        var bobsCookieCopyResponse = await bobsCookieCopy.GetAsync(IdentityServerPipeline.BaseUrl + "/user", _ct);
        bobsCookieCopyResponse.StatusCode.ShouldBe(System.Net.HttpStatusCode.Unauthorized, "a copy of bob's cookie must not reach alice's session");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task changing_users_by_signing_out_and_in_in_one_request_should_not_let_a_copy_of_the_previous_users_cookie_reach_the_new_users_session()
    {
        await _pipeline.LoginAsync("bob");
        var bobsCookieCopy = CopyOfSessionCookie();

        await _pipeline.BrowserClient.GetAsync(IdentityServerPipeline.BaseUrl + "/signout-then-signin-alice", _ct);

        var bobsCookieCopyResponse = await bobsCookieCopy.GetAsync(IdentityServerPipeline.BaseUrl + "/user", _ct);
        bobsCookieCopyResponse.StatusCode.ShouldBe(System.Net.HttpStatusCode.Unauthorized, "a copy of bob's cookie must not reach alice's session");
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

        var ticket = (await _sessionStore.GetSessionsAsync(new SessionFilter { SubjectId = "alice" }, _ct)).Single()
            .Deserialize(_protector, null);
        var claims = ticket.Principal.Claims;
        claims.ShouldContain(c => c.Issuer == "Custom Issuer" && c.Type == "Test");
        claims.ShouldContain(c => c.Issuer == ClaimsIdentity.DefaultIssuer && c.Type == "Test");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task claim_added_by_the_browser_while_a_token_call_extends_the_session_should_be_kept()
    {
        await _pipeline.LoginAsync("alice");
        var refreshToken = await RequestRefreshTokenForCoordinatedClientAsync();

        _sessionReadHold.HoldNextLookupBySubjectAndSession();
        var tokenCallThatExtendsTheSession = RefreshTokensAsync(refreshToken);
        await _sessionReadHold.ReadHeld.Task.WaitAsync(_ct);
        var browserClaimSave = await _pipeline.BrowserClient.GetAsync(IdentityServerPipeline.BaseUrl + "/add-claim-added-by-browser", _ct);

        browserClaimSave.StatusCode.ShouldBe(HttpStatusCode.OK, "precondition: the browser saves the claim while the token call is held");

        _sessionReadHold.Release.SetResult();
        var tokenCallResponse = await tokenCallThatExtendsTheSession;

        tokenCallResponse.IsError.ShouldBeFalse("a token call must succeed when its extension of the session is skipped");
        var storedTicket = (await _sessionStore.GetSessionsAsync(new SessionFilter { SubjectId = "alice" }, _ct)).Single().Deserialize(_protector, null);
        storedTicket.Principal.HasClaim(ClaimAddedByBrowser, "true").ShouldBeTrue("a token call must not overwrite a claim the browser added after the token call read the session");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task app_opened_by_the_browser_while_a_token_call_extends_the_session_should_stay_in_the_client_list()
    {
        await _pipeline.LoginAsync("alice");
        var refreshToken = await RequestRefreshTokenForCoordinatedClientAsync();

        _sessionReadHold.HoldNextLookupBySubjectAndSession();
        var tokenCallThatExtendsTheSession = RefreshTokensAsync(refreshToken);
        await _sessionReadHold.ReadHeld.Task.WaitAsync(_ct);
        var browserAppOpening = await _pipeline.RequestAuthorizationEndpointAsync("client_opt_out", "code", "openid", "https://client_opt_out/callback");

        browserAppOpening.IsError.ShouldBeFalse("precondition: the browser opens client_opt_out while the token call is held");

        _sessionReadHold.Release.SetResult();
        var tokenCallResponse = await tokenCallThatExtendsTheSession;

        tokenCallResponse.IsError.ShouldBeFalse("a token call must succeed when its extension of the session is skipped");
        var aliceSession = (await _ticketService.GetSessionsAsync(new SessionFilter { SubjectId = "alice" }, _ct)).Single();
        aliceSession.ClientIds.ShouldContain("client_opt_out", "a token call must not remove an app the browser opened after the token call read the session");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task claim_added_by_the_browser_while_a_token_call_extends_a_remember_me_session_should_be_kept()
    {
        _pipeline.Options.Authentication.CookieSlidingExpiration = true;
        await _pipeline.LoginAsync("alice", new AuthenticationProperties { IsPersistent = true });
        var refreshToken = await RequestRefreshTokenForCoordinatedClientAsync();

        _sessionReadHold.HoldNextReadByKey();
        var tokenCallThatExtendsTheSession = RefreshTokensAsync(refreshToken);
        await _sessionReadHold.ReadHeld.Task.WaitAsync(_ct);
        var browserClaimSave = await _pipeline.BrowserClient.GetAsync(IdentityServerPipeline.BaseUrl + "/add-claim-added-by-browser", _ct);

        browserClaimSave.StatusCode.ShouldBe(HttpStatusCode.OK, "precondition: the browser saves the claim while the token call is held");

        _sessionReadHold.Release.SetResult();
        var tokenCallResponse = await tokenCallThatExtendsTheSession;

        tokenCallResponse.IsError.ShouldBeFalse("a token call must succeed when its extension of the session is skipped");
        var storedTicket = (await _sessionStore.GetSessionsAsync(new SessionFilter { SubjectId = "alice" }, _ct)).Single().Deserialize(_protector, null);
        storedTicket.Principal.HasClaim(ClaimAddedByBrowser, "true").ShouldBeTrue("a token call must not save a ticket older than the one the browser saved");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task extension_by_a_token_call_while_the_browser_opens_an_app_should_not_be_undone()
    {
        await _pipeline.LoginAsync("alice");
        var refreshToken = await RequestRefreshTokenForCoordinatedClientAsync();
        var expiresBeforeTokenCall = (await _sessionStore.GetSessionsAsync(new SessionFilter { SubjectId = "alice" }, _ct)).Single().Expires!.Value;
        _pipeline.BrowserClient.AllowAutoRedirect = false;

        _sessionReadHold.HoldNextReadByKey();
        var browserOpeningAnApp = _pipeline.BrowserClient.GetAsync(_pipeline.CreateAuthorizeUrl("client_opt_out", "code", "openid", "https://client_opt_out/callback"), _ct);
        await _sessionReadHold.ReadHeld.Task.WaitAsync(_ct);
        var tokenCallResponse = await RefreshTokensAsync(refreshToken);
        var expiresAfterTokenCall = (await _sessionStore.GetSessionsAsync(new SessionFilter { SubjectId = "alice" }, _ct)).Single().Expires!.Value;

        tokenCallResponse.IsError.ShouldBeFalse("precondition: the token call succeeds while the browser is held");
        expiresAfterTokenCall.ShouldBeGreaterThan(expiresBeforeTokenCall, "precondition: the token call extends the session");

        _sessionReadHold.Release.SetResult();
        await Should.ThrowAsync<ServerSideSessionConflictException>(() => browserOpeningAnApp, "the browser request whose save is based on a read from before the extension must fail because its save was refused");

        var expiresAfterBrowserSave = (await _sessionStore.GetSessionsAsync(new SessionFilter { SubjectId = "alice" }, _ct)).Single().Expires!.Value;
        expiresAfterBrowserSave.ShouldBe(expiresAfterTokenCall, "a browser save based on a read from before the extension must not undo the extension");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task two_apps_opened_at_once_should_not_silently_drop_one_from_the_client_list()
    {
        await _pipeline.LoginAsync("alice");
        _pipeline.BrowserClient.AllowAutoRedirect = false;

        _sessionReadHold.HoldNextReadByKey();
        var browserOpeningClientOptOut = _pipeline.BrowserClient.GetAsync(_pipeline.CreateAuthorizeUrl("client_opt_out", "code", "openid", "https://client_opt_out/callback"), _ct);
        await _sessionReadHold.ReadHeld.Task.WaitAsync(_ct);
        var clientInheritOpened = await _pipeline.BrowserClient.GetAsync(_pipeline.CreateAuthorizeUrl("client_inherit", "code", "openid", "https://client_inherit/callback"), _ct);

        clientInheritOpened.StatusCode.ShouldBe(HttpStatusCode.SeeOther, "precondition: client_inherit opens while client_opt_out is held");

        _sessionReadHold.Release.SetResult();
        await Should.ThrowAsync<ServerSideSessionConflictException>(() => browserOpeningClientOptOut, "the browser request whose save is based on a read from before client_inherit opened must fail because its save was refused");

        var aliceSession = (await _ticketService.GetSessionsAsync(new SessionFilter { SubjectId = "alice" }, _ct)).Single();
        aliceSession.ClientIds.ShouldContain("client_inherit", "an app opened at the same time as another must not be dropped from the client list");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task renewal_flagged_by_a_token_call_should_be_skipped_when_a_second_token_call_extends_the_session_during_the_request()
    {
        var refreshToken = await LoginRememberMeAndFlagSessionForRenewalAsync();
        var versionBeforeSecondTokenCall = await StoredVersionAsync();

        _sessionReadHold.HoldNextReadByKey();
        var browserRequest = _pipeline.BrowserClient.GetAsync(IdentityServerPipeline.BaseUrl + "/user", _ct);
        await _sessionReadHold.ReadHeld.Task.WaitAsync(_ct);
        var secondTokenCall = await RefreshTokensAsync(refreshToken);
        var versionAfterSecondTokenCall = await StoredVersionAsync();

        secondTokenCall.IsError.ShouldBeFalse("precondition: the second token call succeeds while the browser request is held");
        versionAfterSecondTokenCall.ShouldBeGreaterThan(versionBeforeSecondTokenCall, "precondition: the second token call saves an extension");

        _sessionReadHold.Release.SetResult();
        var browserResponse = await browserRequest;

        browserResponse.StatusCode.ShouldBe(HttpStatusCode.OK, "the browser request only renewed the dates, so its refused save must be skipped");
        (await StoredVersionAsync()).ShouldBe(versionAfterSecondTokenCall, "the skipped renewal must write nothing, so the extension of the second token call stays the latest save");

        var isLoggedInAfterSkippedRenewal = await IsLoggedIn();

        isLoggedInAfterSkippedRenewal.ShouldBeTrue("the cookie holds only the key, so the session must still authenticate after the skipped renewal");
        (await StoredVersionAsync()).ShouldBeGreaterThan(versionAfterSecondTokenCall, "the next browser request must renew, because the second token call flagged the session again");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task renewal_that_changes_the_session_content_should_still_be_refused_when_a_token_call_extends_the_session_during_the_request()
    {
        var refreshToken = await LoginRememberMeAndFlagSessionForRenewalAsync();
        _validatePrincipalHook = ctx =>
        {
            ((ClaimsIdentity)ctx.Principal.Identity).AddClaim(new Claim(ClaimAddedByBrowser, "true"));
            ctx.ShouldRenew = true;
            return Task.CompletedTask;
        };

        _sessionReadHold.HoldNextReadByKey();
        var browserRequest = _pipeline.BrowserClient.GetAsync(IdentityServerPipeline.BaseUrl + "/user", _ct);
        await _sessionReadHold.ReadHeld.Task.WaitAsync(_ct);
        var secondTokenCall = await RefreshTokensAsync(refreshToken);

        secondTokenCall.IsError.ShouldBeFalse("precondition: the second token call succeeds while the browser request is held");

        _sessionReadHold.Release.SetResult();
        await Should.ThrowAsync<ServerSideSessionConflictException>(() => browserRequest, "a renewal that adds a claim is not dates-only, so its refused save must fail");

        var storedTicket = (await _sessionStore.GetSessionsAsync(new SessionFilter { SubjectId = "alice" }, _ct)).Single().Deserialize(_protector, null);
        storedTicket.Principal.HasClaim(ClaimAddedByBrowser, "true").ShouldBeFalse();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task skipped_dates_only_renewal_should_keep_a_claim_another_browser_request_added_during_the_request()
    {
        await LoginRememberMeAndFlagSessionForRenewalAsync();

        _sessionReadHold.HoldNextReadByKey();
        var browserRequest = _pipeline.BrowserClient.GetAsync(IdentityServerPipeline.BaseUrl + "/user", _ct);
        await _sessionReadHold.ReadHeld.Task.WaitAsync(_ct);
        var claimSave = await _pipeline.BrowserClient.GetAsync(IdentityServerPipeline.BaseUrl + "/add-claim-added-by-browser", _ct);

        claimSave.StatusCode.ShouldBe(HttpStatusCode.OK, "precondition: another browser request saves a claim while the first request is held");

        _sessionReadHold.Release.SetResult();
        var browserResponse = await browserRequest;

        browserResponse.StatusCode.ShouldBe(HttpStatusCode.OK, "the held request only renewed the dates, so its refused save must be skipped");
        var storedTicket = (await _sessionStore.GetSessionsAsync(new SessionFilter { SubjectId = "alice" }, _ct)).Single().Deserialize(_protector, null);
        storedTicket.Principal.HasClaim(ClaimAddedByBrowser, "true").ShouldBeTrue("skipping a dates-only renewal writes nothing, so the claim the other request saved must stay");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task renewal_with_a_replaced_principal_holding_the_same_claims_in_another_order_should_still_be_skipped()
    {
        var refreshToken = await LoginRememberMeAndFlagSessionForRenewalAsync();
        _validatePrincipalHook = ctx =>
        {
            ctx.ReplacePrincipal(new ClaimsPrincipal(new ClaimsIdentity(ctx.Principal.Claims.Reverse().ToArray(), ctx.Principal.Identity.AuthenticationType)));
            ctx.ShouldRenew = true;
            return Task.CompletedTask;
        };

        _sessionReadHold.HoldNextReadByKey();
        var browserRequest = _pipeline.BrowserClient.GetAsync(IdentityServerPipeline.BaseUrl + "/user", _ct);
        await _sessionReadHold.ReadHeld.Task.WaitAsync(_ct);
        var secondTokenCall = await RefreshTokensAsync(refreshToken);

        secondTokenCall.IsError.ShouldBeFalse("precondition: the second token call succeeds while the browser request is held");

        _sessionReadHold.Release.SetResult();
        var browserResponse = await browserRequest;

        browserResponse.StatusCode.ShouldBe(HttpStatusCode.OK, "the order of the claims must not make a dates-only renewal look like a content change");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task renewal_with_a_replaced_principal_that_changes_only_a_claim_value_type_should_still_be_refused()
    {
        var refreshToken = await LoginRememberMeAndFlagSessionForRenewalAsync();
        _validatePrincipalHook = ctx =>
        {
            var identity = (ClaimsIdentity)ctx.Principal.Identity;
            var claimToRetype = identity.Claims.First();
            identity.RemoveClaim(claimToRetype);
            identity.AddClaim(new Claim(claimToRetype.Type, claimToRetype.Value, ClaimValueTypes.Boolean, claimToRetype.Issuer));
            ctx.ShouldRenew = true;
            return Task.CompletedTask;
        };

        _sessionReadHold.HoldNextReadByKey();
        var browserRequest = _pipeline.BrowserClient.GetAsync(IdentityServerPipeline.BaseUrl + "/user", _ct);
        await _sessionReadHold.ReadHeld.Task.WaitAsync(_ct);
        var secondTokenCall = await RefreshTokensAsync(refreshToken);

        secondTokenCall.IsError.ShouldBeFalse("precondition: the second token call succeeds while the browser request is held");

        _sessionReadHold.Release.SetResult();
        await Should.ThrowAsync<ServerSideSessionConflictException>(() => browserRequest, "a renewal that changes a claim's value type is not dates-only, so its refused save must fail");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task renewal_with_a_replaced_principal_that_changes_only_the_identitys_authentication_type_should_still_be_refused()
    {
        var refreshToken = await LoginRememberMeAndFlagSessionForRenewalAsync();
        _validatePrincipalHook = ctx =>
        {
            var identity = (ClaimsIdentity)ctx.Principal.Identity;
            ctx.ReplacePrincipal(new ClaimsPrincipal(new ClaimsIdentity(identity.Claims.ToArray(), identity.AuthenticationType + "-changed")));
            ctx.ShouldRenew = true;
            return Task.CompletedTask;
        };

        _sessionReadHold.HoldNextReadByKey();
        var browserRequest = _pipeline.BrowserClient.GetAsync(IdentityServerPipeline.BaseUrl + "/user", _ct);
        await _sessionReadHold.ReadHeld.Task.WaitAsync(_ct);
        var secondTokenCall = await RefreshTokensAsync(refreshToken);

        secondTokenCall.IsError.ShouldBeFalse("precondition: the second token call succeeds while the browser request is held");

        _sessionReadHold.Release.SetResult();
        await Should.ThrowAsync<ServerSideSessionConflictException>(() => browserRequest, "a renewal that changes the identity's authentication type is not dates-only, so its refused save must fail");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task renewal_that_shortens_the_session_lifetime_should_still_be_refused_when_a_token_call_extends_the_session_during_the_request()
    {
        var refreshToken = await LoginRememberMeAndFlagSessionForRenewalAsync();
        _validatePrincipalHook = ctx =>
        {
            ctx.Properties.ExpiresUtc = ctx.Properties.ExpiresUtc!.Value.AddMinutes(-30);
            ctx.ShouldRenew = true;
            return Task.CompletedTask;
        };

        _sessionReadHold.HoldNextReadByKey();
        var browserRequest = _pipeline.BrowserClient.GetAsync(IdentityServerPipeline.BaseUrl + "/user", _ct);
        await _sessionReadHold.ReadHeld.Task.WaitAsync(_ct);
        var secondTokenCall = await RefreshTokensAsync(refreshToken);

        secondTokenCall.IsError.ShouldBeFalse("precondition: the second token call succeeds while the browser request is held");

        _sessionReadHold.Release.SetResult();
        await Should.ThrowAsync<ServerSideSessionConflictException>(() => browserRequest, "a renewal that shortens the session's lifetime must not be skipped, or the token call's longer end time silently stays");
    }

    private async Task<string> LoginRememberMeAndFlagSessionForRenewalAsync()
    {
        _pipeline.Options.Authentication.CookieSlidingExpiration = true;
        await _pipeline.LoginAsync("alice", new AuthenticationProperties { IsPersistent = true });
        var refreshToken = await RequestRefreshTokenForCoordinatedClientAsync();
        var firstTokenCall = await RefreshTokensAsync(refreshToken);

        firstTokenCall.IsError.ShouldBeFalse("precondition: the first token call succeeds and flags the session for cookie renewal");
        return refreshToken;
    }

    private async Task<int> StoredVersionAsync() =>
        (await _sessionStore.GetSessionsAsync(new SessionFilter { SubjectId = "alice" }, _ct)).Single().Version;

    private BrowserClient CopyOfSessionCookie()
    {
        var sessionCookie = _pipeline.BrowserClient.GetCookie(IdentityServerPipeline.BaseUrl, IdentityServerConstants.DefaultCookieAuthenticationScheme);
        var browser = new BrowserClient(new BrowserHandler(_pipeline.Handler));
        browser.SetCookie(IdentityServerPipeline.BaseUrl, sessionCookie.Name, sessionCookie.Value);
        return browser;
    }

    private async Task<string> RequestRefreshTokenForCoordinatedClientAsync()
    {
        var authorizeResponse = await _pipeline.RequestAuthorizationEndpointAsync("client", "code", "openid api offline_access", "https://client/callback");
        var tokenResponse = await _pipeline.BackChannelClient.RequestAuthorizationCodeTokenAsync(new AuthorizationCodeTokenRequest
        {
            Address = IdentityServerPipeline.TokenEndpoint,
            ClientId = "client",
            Code = authorizeResponse.Code,
            RedirectUri = "https://client/callback"
        }, _ct);

        tokenResponse.IsError.ShouldBeFalse("precondition: the coordinated client gets tokens");
        tokenResponse.RefreshToken.ShouldNotBeNullOrEmpty("precondition: the coordinated client gets a refresh token");
        return tokenResponse.RefreshToken;
    }

    private Task<TokenResponse> RefreshTokensAsync(string refreshToken) =>
        _pipeline.BackChannelClient.RequestRefreshTokenAsync(new RefreshTokenRequest
        {
            Address = IdentityServerPipeline.TokenEndpoint,
            ClientId = "client",
            RefreshToken = refreshToken
        }, _ct);

    private static IServerSideSessionStore CreateRegisteredSessionStore(ServiceDescriptor registeredSessionStore, IServiceProvider serviceProvider) =>
        (IServerSideSessionStore)(registeredSessionStore.ImplementationInstance
            ?? registeredSessionStore.ImplementationFactory?.Invoke(serviceProvider)
            ?? ActivatorUtilities.CreateInstance(serviceProvider, registeredSessionStore.ImplementationType));

    private sealed class SessionReadHold(Ct testCancellation)
    {
        private const int NotHolding = 0;
        private const int HoldingNextReadByKey = 1;
        private const int HoldingNextLookupBySubjectAndSession = 2;

        private int _readToHold = NotHolding;


        public TaskCompletionSource ReadHeld { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void HoldNextReadByKey() => Interlocked.Exchange(ref _readToHold, HoldingNextReadByKey);

        public void HoldNextLookupBySubjectAndSession() => Interlocked.Exchange(ref _readToHold, HoldingNextLookupBySubjectAndSession);

        public Task HoldIfNextReadByKeyAsync() => HoldIfAsync(HoldingNextReadByKey);

        public Task HoldIfNextLookupBySubjectAndSessionAsync() => HoldIfAsync(HoldingNextLookupBySubjectAndSession);

        private async Task HoldIfAsync(int read)
        {
            if (Interlocked.CompareExchange(ref _readToHold, NotHolding, read) == read)
            {
                ReadHeld.SetResult();
                await Release.Task.WaitAsync(testCancellation);
            }
        }
    }

    private sealed class SessionStoreThatCanHoldARead(IServerSideSessionStore registeredStore, SessionReadHold sessionReadHold) : IServerSideSessionStore
    {
        public async Task<ServerSideSession> GetSessionAsync(string key, Ct ct)
        {
            var session = await registeredStore.GetSessionAsync(key, ct);
            await sessionReadHold.HoldIfNextReadByKeyAsync();
            return session;
        }

        public async Task<IReadOnlyCollection<ServerSideSession>> GetSessionsAsync(SessionFilter filter, Ct ct)
        {
            var sessions = await registeredStore.GetSessionsAsync(filter, ct);
            if (filter.SubjectId != null && filter.SessionId != null)
            {
                await sessionReadHold.HoldIfNextLookupBySubjectAndSessionAsync();
            }

            return sessions;
        }

        public Task CreateSessionAsync(ServerSideSession session, Ct ct) => registeredStore.CreateSessionAsync(session, ct);

        public Task<bool> UpdateSessionAsync(ServerSideSession session, Ct ct) => registeredStore.UpdateSessionAsync(session, ct);

        public Task DeleteSessionAsync(string key, Ct ct) => registeredStore.DeleteSessionAsync(key, ct);

        public Task DeleteSessionsAsync(SessionFilter filter, Ct ct) => registeredStore.DeleteSessionsAsync(filter, ct);

        public Task<IReadOnlyCollection<ServerSideSession>> GetAndRemoveExpiredSessionsAsync(int count, Ct ct) => registeredStore.GetAndRemoveExpiredSessionsAsync(count, ct);

        public Task<QueryResult<ServerSideSession>> QuerySessionsAsync(Ct ct, SessionQuery filter = null) => registeredStore.QuerySessionsAsync(ct, filter);
    }
}
