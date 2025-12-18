// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using System.Web;
using Duende.AccessTokenManagement;
using Duende.AccessTokenManagement.OpenIdConnect;
using Duende.IdentityModel;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using RichardSzalay.MockHttp;
using Xunit.Abstractions;

namespace Duende.AspNetCore.Authentication.JwtBearer.TestFramework;

public class AppHost : GenericHost
{
    private readonly IdentityServerHost _identityServerHost;
    private readonly ApiHost _apiHost;
    private readonly string _clientId;
    private readonly Action<UserTokenManagementOptions>? _configureUserTokenManagementOptions;

    public AppHost(
        IdentityServerHost identityServerHost,
        ApiHost apiHost,
        string clientId,
        ITestOutputHelper testOutputHelper,
        string baseAddress = "https://app",
        Action<UserTokenManagementOptions>? configureUserTokenManagementOptions = default)
        : base(testOutputHelper, baseAddress)
    {
        _identityServerHost = identityServerHost;
        _apiHost = apiHost;
        _clientId = clientId;
        _configureUserTokenManagementOptions = configureUserTokenManagementOptions;
        OnConfigureServices += ConfigureServices;
        OnConfigure += Configure;
    }

    public MockHttpMessageHandler? IdentityServerHttpHandler { get; set; }

    private void ConfigureServices(IServiceCollection services)
    {
        services.AddRouting();
        services.AddAuthorization();

        services.AddAuthentication(options =>
            {
                options.DefaultScheme = "cookie";
                options.DefaultChallengeScheme = "oidc";
                options.DefaultSignOutScheme = "oidc";
            })
            .AddCookie("cookie")
            .AddOpenIdConnect("oidc", options =>
            {
                options.Authority = _identityServerHost.Url();

                options.ClientId = _clientId;
                options.ClientSecret = "secret";
                options.ResponseType = "code";
                options.ResponseMode = "query";

                options.DisableTelemetry = true;
                options.MapInboundClaims = false;
                options.GetClaimsFromUserInfoEndpoint = false;
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

                var identityServerHandler = _identityServerHost.Server.CreateHandler();
                if (IdentityServerHttpHandler != null)
                {
                    // allow discovery document
                    IdentityServerHttpHandler.When("/.well-known/*")
                        .Respond(identityServerHandler);

                    options.BackchannelHttpHandler = IdentityServerHttpHandler;
                }
                else
                {
                    options.BackchannelHttpHandler = identityServerHandler;
                }

                options.ProtocolValidator.RequireNonce = false;
            });

        services.AddDistributedMemoryCache();
        services.AddOpenIdConnectAccessTokenManagement(opt =>
        {
            _configureUserTokenManagementOptions?.Invoke(opt);
        });

    }

    private void Configure(IApplicationBuilder app)
    {
        app.UseAuthentication();
        app.UseRouting();
        app.UseAuthorization();

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapGet("/login", async context =>
            {
                await context.ChallengeAsync(new AuthenticationProperties
                {
                    RedirectUri = "/"
                });
            });

            endpoints.MapGet("/logout", async context =>
            {
                await context.SignOutAsync();
            });

            endpoints.MapGet("/user_token", async context =>
            {
                var tokenResult = await context.GetUserAccessTokenAsync();
                await context.Response.WriteAsJsonAsync(tokenResult.Token);
            });

            endpoints.MapGet("/user_token_with_resource/{resource}", async (string resource, HttpContext context) =>
            {
                var token = await context.GetUserAccessTokenAsync(new UserTokenRequestParameters
                {
                    Resource = Resource.Parse(resource)
                });
                await context.Response.WriteAsJsonAsync(token);
            });

            endpoints.MapGet("/client_token", async context =>
            {
                var token = await context.GetClientAccessTokenAsync();
                await context.Response.WriteAsJsonAsync(token);
            });
        });
    }

    public async Task<HttpResponseMessage> LoginAsync(string sub, string? sid = null, bool verifyDpopThumbprintSent = false)
    {
        await _identityServerHost.CreateIdentityServerSessionCookieAsync(sub, sid);
        return await OidcLoginAsync(verifyDpopThumbprintSent);
    }

    public async Task<HttpResponseMessage> OidcLoginAsync(bool verifyDpopThumbprintSent)
    {
        var response = await BrowserClient.GetAsync(Url("/login"));
        response.StatusCode.ShouldBe((HttpStatusCode)302); // authorize
        response.Headers.Location!.ToString().ToLowerInvariant().ShouldStartWith(_identityServerHost.Url("/connect/authorize"));

        if (verifyDpopThumbprintSent)
        {
            var queryParams = HttpUtility.ParseQueryString(response.Headers.Location.Query);
            queryParams.AllKeys.ShouldContain(OidcConstants.AuthorizeRequest.DPoPKeyThumbprint);
        }

        response = await _identityServerHost.BrowserClient.GetAsync(response.Headers.Location.ToString());
        response.StatusCode.ShouldBe((HttpStatusCode)302); // client callback
        response.Headers.Location!.ToString().ToLowerInvariant().ShouldStartWith(Url("/signin-oidc"));

        response = await BrowserClient.GetAsync(response.Headers.Location.ToString());
        response.StatusCode.ShouldBe((HttpStatusCode)302); // root
        response.Headers.Location!.ToString().ToLowerInvariant().ShouldBe("/");

        response = await BrowserClient.GetAsync(Url(response.Headers.Location.ToString()));
        return response;
    }

    public async Task<HttpResponseMessage> LogoutAsync(string? sid = null)
    {
        var response = await BrowserClient.GetAsync(Url("/logout") + "?sid=" + sid);
        response.StatusCode.ShouldBe((HttpStatusCode)302); // endsession
        response.Headers.Location!.ToString().ToLowerInvariant().ShouldStartWith(_identityServerHost.Url("/connect/endsession"));

        response = await _identityServerHost.BrowserClient.GetAsync(response.Headers.Location.ToString());
        response.StatusCode.ShouldBe((HttpStatusCode)302); // logout
        response.Headers.Location!.ToString().ToLowerInvariant().ShouldStartWith(_identityServerHost.Url("/account/logout"));

        response = await _identityServerHost.BrowserClient.GetAsync(response.Headers.Location.ToString());
        response.StatusCode.ShouldBe((HttpStatusCode)302); // post logout redirect uri
        response.Headers.Location!.ToString().ToLowerInvariant().ShouldStartWith(Url("/signout-callback-oidc"));

        response = await BrowserClient.GetAsync(response.Headers.Location.ToString());
        response.StatusCode.ShouldBe((HttpStatusCode)302); // root
        response.Headers.Location!.ToString().ToLowerInvariant().ShouldBe("/");

        response = await BrowserClient.GetAsync(Url(response.Headers.Location.ToString()));
        return response;
    }
}
