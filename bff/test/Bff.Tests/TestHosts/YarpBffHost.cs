// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using Duende.Bff.Tests.TestFramework;
using Duende.Bff.Yarp;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.DependencyInjection;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Forwarder;

namespace Duende.Bff.Tests.TestHosts;

public class YarpBffHost : GenericHost
{
    public enum ResponseStatus
    {
        Ok,
        Challenge,
        Forbid
    }

    public ResponseStatus LocalApiResponseStatus { get; set; } = ResponseStatus.Ok;

    private readonly IdentityServerHost _identityServerHost;
    private readonly ApiHost _apiHost;
    private readonly string _clientId;
    private readonly bool _useForwardedHeaders;

    public BffOptions BffOptions { get; private set; } = null!;

    public YarpBffHost(
        WriteTestOutput output,
        IdentityServerHost identityServerHost,
        ApiHost apiHost,
        string clientId,
        string baseAddress = "https://app",
        bool useForwardedHeaders = false)
        : base(output, baseAddress)
    {
        _identityServerHost = identityServerHost;
        _apiHost = apiHost;
        _clientId = clientId;
        _useForwardedHeaders = useForwardedHeaders;

        OnConfigureServices += ConfigureServices;
        OnConfigure += Configure;
    }

    private void ConfigureServices(IServiceCollection services)
    {
        services.AddRouting();
        services.AddAuthorization();

        var bff = services.AddBff(options => { BffOptions = options; });

        services.AddSingleton<IForwarderHttpClientFactory>(
            new CallbackForwarderHttpClientFactory(
                context => new HttpMessageInvoker(_apiHost.Server.CreateHandler())));

        var yarpBuilder = services.AddReverseProxy()
            .AddBffExtensions();

        yarpBuilder.LoadFromMemory(
            new[]
            {
                new RouteConfig
                {
                    RouteId = "api_anon_no_csrf",
                    ClusterId = "cluster1",

                    Match = new RouteMatch
                    {
                        Path = "/api_anon_no_csrf/{**catch-all}"
                    }
                },

                new RouteConfig
                {
                    RouteId = "api_anon",
                    ClusterId = "cluster1",

                    Match = new RouteMatch
                    {
                        Path = "/api_anon/{**catch-all}"
                    }
                }.WithAntiforgeryCheck(),

                new RouteConfig
                    {
                        RouteId = "api_user",
                        ClusterId = "cluster1",

                        Match = new RouteMatch
                        {
                            Path = "/api_user/{**catch-all}"
                        }
                    }.WithAntiforgeryCheck()
                    .WithAccessToken(TokenType.User),

                new RouteConfig
                    {
                        RouteId = "api_optional_user",
                        ClusterId = "cluster1",

                        Match = new RouteMatch
                        {
                            Path = "/api_optional_user/{**catch-all}"
                        }
                    }.WithAntiforgeryCheck()
                    .WithOptionalUserAccessToken(),

                new RouteConfig
                    {
                        RouteId = "api_client",
                        ClusterId = "cluster1",

                        Match = new RouteMatch
                        {
                            Path = "/api_client/{**catch-all}"
                        }
                    }.WithAntiforgeryCheck()
                    .WithAccessToken(TokenType.Client),

                new RouteConfig
                    {
                        RouteId = "api_user_or_client",
                        ClusterId = "cluster1",

                        Match = new RouteMatch
                        {
                            Path = "/api_user_or_client/{**catch-all}"
                        }
                    }.WithAntiforgeryCheck()
                    .WithAccessToken(TokenType.UserOrClient),

                // This route configuration is invalid. WithAccessToken says
                // that the access token is required, while
                // WithOptionalUserAccessToken says that it is optional.
                // Calling this endpoint results in a run time error.
                new RouteConfig
                    {
                        RouteId = "api_invalid",
                        ClusterId = "cluster1",

                        Match = new RouteMatch
                        {
                            Path = "/api_invalid/{**catch-all}"
                        }
                    }.WithOptionalUserAccessToken()
                    .WithAccessToken(TokenType.User)
            },
            new[]
            {
                new ClusterConfig
                {
                    ClusterId = "cluster1",

                    Destinations = new Dictionary<string, DestinationConfig>(StringComparer.OrdinalIgnoreCase)
                    {
                        { "destination1", new DestinationConfig { Address = _apiHost.Url() } }
                    }
                }
            });

        // todo: need YARP equivalent
        // services.AddSingleton<IHttpMessageInvokerFactory>(
        //     new CallbackHttpMessageInvokerFactory(
        //         path => new HttpMessageInvoker(_apiHost.Server.CreateHandler())));

        services.AddAuthentication("cookie")
            .AddCookie("cookie", options => { options.Cookie.Name = "bff"; });

        bff.AddServerSideSessions();

        services.AddAuthentication(options =>
            {
                options.DefaultChallengeScheme = "oidc";
                options.DefaultSignOutScheme = "oidc";
            })
            .AddOpenIdConnect("oidc",
                options =>
                {
                    options.Authority = _identityServerHost.Url();

                    options.ClientId = _clientId;
                    options.ClientSecret = "secret";
                    options.ResponseType = "code";
                    options.ResponseMode = "query";

                    options.MapInboundClaims = false;
                    options.GetClaimsFromUserInfoEndpoint = true;
                    options.SaveTokens = true;

                    options.Scope.Clear();
                    var client = _identityServerHost.Clients.Single(x => x.ClientId == _clientId);
                    foreach (var scope in client.AllowedScopes)
                    {
                        options.Scope.Add(scope);
                    }

                    if (client.AllowOfflineAccess)
                    {
                        options.Scope.Add("offline_access");
                    }

                    options.BackchannelHttpHandler = _identityServerHost.Server.CreateHandler();
                });

        services.AddAuthorization(options =>
        {
            options.AddPolicy("AlwaysFail", policy => { policy.RequireAssertion(ctx => false); });
        });
    }

    private void Configure(IApplicationBuilder app)
    {
        if (_useForwardedHeaders)
        {
            app.UseForwardedHeaders(new ForwardedHeadersOptions
            {
                ForwardedHeaders = ForwardedHeaders.XForwardedFor |
                                   ForwardedHeaders.XForwardedProto |
                                   ForwardedHeaders.XForwardedHost
            });
        }

        app.UseAuthentication();

        app.UseRouting();

        app.UseBff();
        app.UseAuthorization();

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapBffManagementEndpoints();

            endpoints.MapReverseProxy(proxyApp => { proxyApp.UseAntiforgeryCheck(); });
        });
    }

    public async Task<bool> GetIsUserLoggedInAsync(string? userQuery = null)
    {
        if (userQuery != null)
        {
            userQuery = '?' + userQuery;
        }

        var req = new HttpRequestMessage(HttpMethod.Get, Url("/bff/user") + userQuery);
        req.Headers.Add("x-csrf", "1");
        var response = await BrowserClient.SendAsync(req);

        (response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.Unauthorized)
            .ShouldBeTrue();

        return response.StatusCode == HttpStatusCode.OK;
    }

    public async Task<HttpResponseMessage> BffLoginAsync(string sub, string? sid = null)
    {
        await _identityServerHost.CreateIdentityServerSessionCookieAsync(sub, sid);
        return await BffOidcLoginAsync();
    }

    public async Task<HttpResponseMessage> BffOidcLoginAsync()
    {
        var response = await BrowserClient.GetAsync(Url("/bff/login"));
        response.StatusCode.ShouldBe(HttpStatusCode.Redirect); // authorize
        response.Headers.Location!.ToString().ToLowerInvariant()
            .ShouldStartWith(_identityServerHost.Url("/connect/authorize"));

        response = await _identityServerHost.BrowserClient.GetAsync(response.Headers.Location.ToString());
        response.StatusCode.ShouldBe(HttpStatusCode.Redirect); // client callback
        response.Headers.Location!.ToString().ToLowerInvariant().ShouldStartWith(Url("/signin-oidc"));

        response = await BrowserClient.GetAsync(response.Headers.Location.ToString());
        response.StatusCode.ShouldBe(HttpStatusCode.Redirect); // root
        response.Headers.Location!.ToString().ToLowerInvariant().ShouldBe("/");

        (await GetIsUserLoggedInAsync()).ShouldBeTrue();

        response = await BrowserClient.GetAsync(Url(response.Headers.Location.ToString()));
        return response;
    }

    public async Task<HttpResponseMessage> BffLogoutAsync(string? sid = null)
    {
        var response = await BrowserClient.GetAsync(Url("/bff/logout") + "?sid=" + sid);
        response.StatusCode.ShouldBe(HttpStatusCode.Redirect); // endsession
        response.Headers.Location!.ToString().ToLowerInvariant()
            .ShouldStartWith(_identityServerHost.Url("/connect/endsession"));

        response = await _identityServerHost.BrowserClient.GetAsync(response.Headers.Location.ToString());
        response.StatusCode.ShouldBe(HttpStatusCode.Redirect); // logout
        response.Headers.Location!.ToString().ToLowerInvariant()
            .ShouldStartWith(_identityServerHost.Url("/account/logout"));

        response = await _identityServerHost.BrowserClient.GetAsync(response.Headers.Location.ToString());
        response.StatusCode.ShouldBe(HttpStatusCode.Redirect); // post logout redirect uri
        response.Headers.Location!.ToString().ToLowerInvariant().ShouldStartWith(Url("/signout-callback-oidc"));

        response = await BrowserClient.GetAsync(response.Headers.Location.ToString());
        response.StatusCode.ShouldBe(HttpStatusCode.Redirect); // root
        response.Headers.Location!.ToString().ToLowerInvariant().ShouldBe("/");

        (await GetIsUserLoggedInAsync()).ShouldBeFalse();

        response = await BrowserClient.GetAsync(Url(response.Headers.Location.ToString()));
        return response;
    }

    public class CallbackForwarderHttpClientFactory : IForwarderHttpClientFactory
    {
        public Func<ForwarderHttpClientContext, HttpMessageInvoker> CreateInvoker { get; set; }

        public CallbackForwarderHttpClientFactory(Func<ForwarderHttpClientContext, HttpMessageInvoker> callback)
        {
            CreateInvoker = callback;
        }

        public HttpMessageInvoker CreateClient(ForwarderHttpClientContext context)
        {
            return CreateInvoker.Invoke(context);
        }
    }
}
