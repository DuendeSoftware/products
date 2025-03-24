// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Net;
using Duende.IdentityServer;
using Duende.IdentityServer.Hosting.DynamicProviders;
using Duende.IdentityServer.IntegrationTests.TestFramework;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IdentityServer.IntegrationTests.Hosting;

public class DynamicProvidersTests
{
    private GenericHost _host;
    private GenericHost _idp1;
    private GenericHost _idp2;

    private List<OidcProvider> _oidcProviders = new List<OidcProvider>()
    {
        new OidcProvider
        {
            Scheme = "idp1",
            Authority = "https://idp1",
            ClientId = "client",
            ClientSecret = "secret",
            ResponseType = "code",
        }
    };

    public string Idp1FrontChannelLogoutUri { get; set; }

    public DynamicProvidersTests()
    {
        _idp1 = new GenericHost("https://idp1");
        _idp1.OnConfigureServices += services =>
        {
            services.AddRouting();
            services.AddAuthorization();

            services.AddIdentityServer()
                .AddInMemoryClients(new Client[] {
                    new Client
                    {
                        ClientId = "client",
                        ClientSecrets = { new Secret("secret".Sha256()) },
                        AllowedGrantTypes = GrantTypes.Code,
                        RedirectUris = { "https://server/federation/idp1/signin" },
                        PostLogoutRedirectUris = { "https://server/federation/idp1/signout-callback" },
                        FrontChannelLogoutUri = "https://server/federation/idp1/signout",
                        AllowedScopes = { "openid" }
                    }
                })
                .AddInMemoryIdentityResources(new IdentityResource[] {
                    new IdentityResources.OpenId(),
                })
                .AddDeveloperSigningCredential(persistKey: false);

            services.AddLogging(options =>
            {
                options.AddFilter("Duende", LogLevel.Debug);
            });
        };
        _idp1.OnConfigure += app =>
        {
            app.UseRouting();

            app.UseIdentityServer();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGet("/signin", async ctx =>
                {
                    await ctx.SignInAsync(new IdentityServerUser("1").CreatePrincipal());
                });
                endpoints.MapGet("/account/logout", async ctx =>
                {
                    var isis = ctx.RequestServices.GetRequiredService<IIdentityServerInteractionService>();
                    var logoutCtx = await isis.GetLogoutContextAsync(ctx.Request.Query["logoutId"]);
                    Idp1FrontChannelLogoutUri = logoutCtx.SignOutIFrameUrl;
                    await ctx.SignOutAsync();
                });
            });
        };
        _idp1.InitializeAsync().Wait();

        _idp2 = new GenericHost("https://idp2");
        _idp2.OnConfigureServices += services =>
        {
            services.AddRouting();
            services.AddAuthorization();

            services.AddIdentityServer()
                .AddInMemoryClients(new Client[] {
                    new Client
                    {
                        ClientId = "client",
                        ClientSecrets = { new Secret("secret".Sha256()) },
                        AllowedGrantTypes = GrantTypes.Code,
                        RedirectUris = { "https://server/signin-oidc" },
                        PostLogoutRedirectUris = { "https://server/signout-callback-oidc" },
                        FrontChannelLogoutUri = "https://server/signout-oidc",
                        AllowedScopes = { "openid" }
                    }
                })
                .AddInMemoryIdentityResources(new IdentityResource[] {
                    new IdentityResources.OpenId(),
                })
                .AddDeveloperSigningCredential(persistKey: false);

            services.AddLogging(options =>
            {
                options.AddFilter("Duende", LogLevel.Debug);
            });
        };
        _idp2.OnConfigure += app =>
        {
            app.UseRouting();

            app.UseIdentityServer();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGet("/signin", async ctx =>
                {
                    await ctx.SignInAsync(new IdentityServerUser("2").CreatePrincipal());
                });
            });
        };
        _idp2.InitializeAsync().Wait();



        _host = new GenericHost("https://server");
        _host.OnConfigureServices += services =>
        {
            services.AddRouting();
            services.AddAuthorization();

            services.AddIdentityServer()
                .AddInMemoryClients(new Client[] { })
                .AddInMemoryIdentityResources(new IdentityResource[] { })
                .AddInMemoryOidcProviders(_oidcProviders)
                .AddInMemoryCaching()
                .AddIdentityProviderStoreCache<InMemoryIdentityProviderStore>()
                .AddDeveloperSigningCredential(persistKey: false);

            services.ConfigureAll<OpenIdConnectOptions>(options =>
            {
                options.BackchannelHttpHandler = _idp1.Server.CreateHandler();
            });

            services.AddAuthentication()
                .AddOpenIdConnect("idp2", options =>
                {
                    options.SignInScheme = IdentityServerConstants.ExternalCookieAuthenticationScheme;
                    options.Authority = "https://idp2";
                    options.ClientId = "client";
                    options.ClientSecret = "secret";
                    options.ResponseType = "code";
                    options.ResponseMode = "query";
                    options.Scope.Clear();
                    options.Scope.Add("openid");
                    options.MapInboundClaims = false;
                    options.BackchannelHttpHandler = _idp2.Server.CreateHandler();
                });

            services.AddLogging(options =>
            {
                options.AddFilter("Duende", LogLevel.Debug);
            });
        };
        _host.OnConfigure += app =>
        {
            app.UseRouting();

            app.UseIdentityServer();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGet("/user", async ctx =>
                {
                    var session = await ctx.AuthenticateAsync(IdentityServerConstants.DefaultCookieAuthenticationScheme);
                    if (session.Succeeded)
                    {
                        await ctx.Response.WriteAsync(session.Principal.FindFirst("sub").Value);
                    }
                    else
                    {
                        ctx.Response.StatusCode = 401;
                    }
                });
                endpoints.MapGet("/callback", async ctx =>
                {
                    var session = await ctx.AuthenticateAsync(IdentityServerConstants.ExternalCookieAuthenticationScheme);
                    if (session.Succeeded)
                    {
                        await ctx.SignInAsync(session.Principal, session.Properties);
                        await ctx.SignOutAsync(IdentityServerConstants.ExternalCookieAuthenticationScheme);

                        await ctx.Response.WriteAsync(session.Principal.FindFirst("sub").Value);
                    }
                    else
                    {
                        ctx.Response.StatusCode = 401;
                    }
                });
                endpoints.MapGet("/challenge", async ctx =>
                {
                    await ctx.ChallengeAsync(ctx.Request.Query["scheme"],
                        new AuthenticationProperties { RedirectUri = "/callback" });
                });
                endpoints.MapGet("/logout", async ctx =>
                {
                    await ctx.SignOutAsync(ctx.Request.Query["scheme"]);
                });
            });
        };

        _host.InitializeAsync().Wait();
    }

    [Fact]
    public async Task challenge_should_trigger_authorize_request_to_dynamic_idp()
    {
        var response = await _host.HttpClient.GetAsync(_host.Url("/challenge?scheme=idp1"));

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location.ToString().ShouldStartWith("https://idp1/connect/authorize");
    }

    [Fact]
    public async Task signout_should_trigger_endsession_request_to_dynamic_idp()
    {
        var response = await _host.HttpClient.GetAsync(_host.Url("/logout?scheme=idp1"));

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location.ToString().ShouldStartWith("https://idp1/connect/endsession");
    }

    [Fact]
    public async Task challenge_should_trigger_authorize_request_to_static_idp()
    {
        var response = await _host.HttpClient.GetAsync(_host.Url("/challenge?scheme=idp2"));

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location.ToString().ShouldStartWith("https://idp2/connect/authorize");
    }

#if NET5_0_OR_GREATER
    // the cookie processing in this workflow requires updates to .NET5 for our test browser and cookie container
    // https://github.com/dotnet/runtime/issues/26776

    [Fact]
    public async Task redirect_uri_should_process_dynamic_provider_signin_result()
    {
        var response = await _host.BrowserClient.GetAsync(_host.Url("/challenge?scheme=idp1"));
        var authzUrl = response.Headers.Location.ToString();

        await _idp1.BrowserClient.GetAsync(_idp1.Url("/signin"));
        response = await _idp1.BrowserClient.GetAsync(authzUrl);
        var redirectUri = response.Headers.Location.ToString();
        redirectUri.ShouldStartWith("https://server/federation/idp1/signin");

        response = await _host.BrowserClient.GetAsync(redirectUri);
        response.Headers.Location.ToString().ShouldStartWith("/callback");

        response = await _host.BrowserClient.GetAsync(_host.Url("/callback"));
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.ShouldBe("1"); // sub
    }

    [Fact]
    public async Task redirect_uri_should_process_static_provider_signin_result()
    {
        var response = await _host.BrowserClient.GetAsync(_host.Url("/challenge?scheme=idp2"));
        var authzUrl = response.Headers.Location.ToString();

        await _idp2.BrowserClient.GetAsync(_idp2.Url("/signin"));
        response = await _idp2.BrowserClient.GetAsync(authzUrl);
        var redirectUri = response.Headers.Location.ToString();
        redirectUri.ShouldStartWith("https://server/signin-oidc");

        response = await _host.BrowserClient.GetAsync(redirectUri);
        response = await _host.BrowserClient.GetAsync(_host.Url(response.Headers.Location.ToString())); // ~/callback
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.ShouldBe("2"); // sub
    }

    [Fact]
    public async Task redirect_uri_should_work_when_dynamic_provider_not_in_cache()
    {
        var response = await _host.BrowserClient.GetAsync(_host.Url("/challenge?scheme=idp1"));
        var authzUrl = response.Headers.Location.ToString();

        await _idp1.BrowserClient.GetAsync(_idp1.Url("/signin"));
        response = await _idp1.BrowserClient.GetAsync(authzUrl);
        var redirectUri = response.Headers.Location.ToString();
        redirectUri.ShouldStartWith("https://server/federation/idp1/signin");

        var cache = _host.Resolve<ICache<IdentityProvider>>() as DefaultCache<IdentityProvider>;
        await cache.RemoveAsync("test");

        response = await _host.BrowserClient.GetAsync(redirectUri);

        response = await _host.BrowserClient.GetAsync(_host.Url(response.Headers.Location.ToString()));
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.ShouldBe("1"); // sub
    }

    [Fact]
    public async Task front_channel_signout_from_dynamic_idp_should_sign_user_out()
    {
        var response = await _host.BrowserClient.GetAsync(_host.Url("/challenge?scheme=idp1"));
        var authzUrl = response.Headers.Location.ToString();

        await _idp1.BrowserClient.GetAsync(_idp1.Url("/signin"));
        response = await _idp1.BrowserClient.GetAsync(authzUrl); // ~idp1/connect/authorize
        var redirectUri = response.Headers.Location.ToString();

        response = await _host.BrowserClient.GetAsync(redirectUri); // federation/idp1/signin
        response = await _host.BrowserClient.GetAsync(_host.Url("/callback")); // signs the user in

        response = await _host.BrowserClient.GetAsync(_host.Url("/user"));
        response.StatusCode.ShouldBe(HttpStatusCode.OK);


        response = await _host.BrowserClient.GetAsync(_host.Url("/logout?scheme=idp1"));
        var endSessionUrl = response.Headers.Location.ToString();

        response = await _idp1.BrowserClient.GetAsync(endSessionUrl);
        response = await _idp1.BrowserClient.GetAsync(response.Headers.Location.ToString()); // ~/idp1/account/logout

        var page = await _idp1.BrowserClient.GetAsync(Idp1FrontChannelLogoutUri);
        var iframeUrl = await _idp1.BrowserClient.ReadElementAttributeAsync("iframe", "src");

        response = await _host.BrowserClient.GetAsync(_host.Url("/user"));
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        iframeUrl.ShouldStartWith(_host.Url("/federation/idp1/signout"));
        response = await _host.BrowserClient.GetAsync(iframeUrl); // ~/federation/idp1/signout
        response.IsSuccessStatusCode.ShouldBeTrue();

        response = await _host.BrowserClient.GetAsync(_host.Url("/user"));
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
#endif

    [Fact]
    public async Task missing_segments_in_redirect_uri_should_return_not_found()
    {
        var response = await _host.HttpClient.GetAsync(_host.Url("/federation/idp1"));
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
    [Fact]
    public async Task federation_endpoint_with_no_scheme_should_return_not_found()
    {
        var response = await _host.HttpClient.GetAsync(_host.Url("/federation"));
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
