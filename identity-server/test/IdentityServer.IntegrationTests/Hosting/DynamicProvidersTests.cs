// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Net;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Hosting.DynamicProviders;
using Duende.IdentityServer.IntegrationTests.TestFramework;
using Duende.IdentityServer.IntegrationTests.TestFramework.TestIsolation;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Services.Default;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.IntegrationTests.Hosting;

public sealed class DynamicProvidersTests : IAsyncLifetime
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;
    private readonly WebServerFixture _webServerFixture;
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

    private Action<IdentityServerOptions> _configureIdentityServerOptions = options => { };

    public DynamicProvidersTests(WebServerFixture webServerFixture)
    {
        _webServerFixture = webServerFixture;

        _idp1 = new GenericHost("https://idp1");
        _idp1.OnConfigureServices += services =>
        {
            services.AddRouting();
            services.AddAuthorization();

            services.AddIdentityServer(_configureIdentityServerOptions)
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

            app.MapGet("/signin", async ctx =>
            {
                await ctx.SignInAsync(new IdentityServerUser("1").CreatePrincipal());
            });

            app.MapGet("/account/logout", async ctx =>
            {
                var isis = ctx.RequestServices.GetRequiredService<IIdentityServerInteractionService>();
                var logoutCtx = await isis.GetLogoutContextAsync(ctx.Request.Query["logoutId"], ctx.RequestAborted);
                Idp1FrontChannelLogoutUri = logoutCtx.SignOutIFrameUrl;
                await ctx.SignOutAsync();
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

            app.MapGet("/signin", async ctx =>
            {
                await ctx.SignInAsync(new IdentityServerUser("2").CreatePrincipal());
            });
        };
        _idp2.InitializeAsync().Wait();



        _host = new GenericHost("https://server");
        _host.OnConfigureServices += services =>
        {
            services.AddRouting();
            services.AddAuthorization();

            services.AddIdentityServer(_configureIdentityServerOptions)
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

            app.MapGet("/user", async ctx =>
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

            app.MapGet("/callback", async ctx =>
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

            app.MapGet("/challenge", async ctx =>
            {
                await ctx.ChallengeAsync(ctx.Request.Query["scheme"],
                    new AuthenticationProperties { RedirectUri = "/callback" });
            });

            app.MapGet("/logout", async ctx =>
            {
                await ctx.SignOutAsync(ctx.Request.Query["scheme"]);
            });
        };
    }

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task challenge_should_trigger_authorize_request_to_dynamic_idp()
    {
        await _host.InitializeAsync();

        var response = await _host.HttpClient.GetAsync(_host.Url("/challenge?scheme=idp1"));

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location.ToString().ShouldStartWith("https://idp1/connect/authorize");
    }

    [Fact]
    public async Task signout_should_trigger_endsession_request_to_dynamic_idp()
    {
        await _host.InitializeAsync();

        var response = await _host.HttpClient.GetAsync(_host.Url("/logout?scheme=idp1"));

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location.ToString().ShouldStartWith("https://idp1/connect/endsession");
    }

    [Fact]
    public async Task challenge_should_trigger_authorize_request_to_static_idp()
    {
        await _host.InitializeAsync();

        var response = await _host.HttpClient.GetAsync(_host.Url("/challenge?scheme=idp2"));

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location.ToString().ShouldStartWith("https://idp2/connect/authorize");
    }

    // the cookie processing in this workflow requires updates to .NET5 for our test browser and cookie container
    // https://github.com/dotnet/runtime/issues/26776

    [Theory]
    [ClassData(typeof(DynamicProviderConfigurationData))]
    public async Task redirect_uri_should_process_dynamic_provider_signin_result(DynamicProviderTestScenario testScenario)
    {
        _configureIdentityServerOptions = testScenario.ConfigureOptions;
        await _host.InitializeAsync();

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
        await _host.InitializeAsync();

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

    [Theory]
    [ClassData(typeof(DynamicProviderConfigurationData))]
    public async Task redirect_uri_should_work_when_dynamic_provider_not_in_cache(DynamicProviderTestScenario testScenario)
    {
        _configureIdentityServerOptions = testScenario.ConfigureOptions;
        await _host.InitializeAsync();

        var response = await _host.BrowserClient.GetAsync(_host.Url("/challenge?scheme=idp1"));
        var authzUrl = response.Headers.Location.ToString();

        await _idp1.BrowserClient.GetAsync(_idp1.Url("/signin"));
        response = await _idp1.BrowserClient.GetAsync(authzUrl);
        var redirectUri = response.Headers.Location.ToString();
        redirectUri.ShouldStartWith("https://server/federation/idp1/signin");

        var cache = _host.Resolve<HybridCache>(ServiceProviderKeys.ConfigurationStoreCache);
        var cachePolicy = _host.Resolve<CachePolicy<IdentityProvider>>();
        await cache.RemoveAsync(cachePolicy.BuildKey("idp1"), _ct);

        response = await _host.BrowserClient.GetAsync(redirectUri);

        response = await _host.BrowserClient.GetAsync(_host.Url(response.Headers.Location.ToString()));
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.ShouldBe("1"); // sub
    }

    [Theory]
    [ClassData(typeof(DynamicProviderConfigurationData))]
    public async Task front_channel_signout_from_dynamic_idp_should_sign_user_out(DynamicProviderTestScenario testScenario)
    {
        _configureIdentityServerOptions = testScenario.ConfigureOptions;
        await _host.InitializeAsync();

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


    [Theory]
    [ClassData(typeof(DynamicProviderConfigurationData))]
    public async Task missing_segments_in_redirect_uri_should_return_not_found(DynamicProviderTestScenario testScenario)
    {
        _configureIdentityServerOptions = testScenario.ConfigureOptions;
        await _host.InitializeAsync();

        var response = await _host.HttpClient.GetAsync(_host.Url("/federation/idp1"));
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Theory]
    [ClassData(typeof(DynamicProviderConfigurationData))]
    public async Task federation_endpoint_with_no_scheme_should_return_not_found(DynamicProviderTestScenario testScenario)
    {
        _configureIdentityServerOptions = testScenario.ConfigureOptions;
        await _host.InitializeAsync();

        var response = await _host.HttpClient.GetAsync(_host.Url("/federation"));
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // Note: this extra little level of indirection is needed to ensure that the test scenarios show
    // test names that are meaningful and not the result of the ToString() method on the Action delegate.
    public record DynamicProviderTestScenario(string Name, Action<IdentityServerOptions> ConfigureOptions)
    {
        public override string ToString() => Name;
    }

    private class DynamicProviderConfigurationData : TheoryData<DynamicProviderTestScenario>
    {
        public DynamicProviderConfigurationData()
        {
            Add(new DynamicProviderTestScenario("Default PathPrefix", _ => { }));
            Add(new DynamicProviderTestScenario("PathPrefix Callback",
                options => options.DynamicProviders.PathMatchingCallback = ctx =>
                {
                    if (ctx.Request.Path.StartsWithSegments("/federation/idp1", StringComparison.InvariantCulture))
                    {
                        return Task.FromResult("idp1");
                    }

                    return Task.FromResult((string)null);
                }));
        }
    }

    private sealed class NoSpacesFixture : IAsyncDisposable
    {
        public required KestrelBasedTestServer MainServer { get; init; }
        public required KestrelBasedTestServer IdpServer { get; init; }

        public async ValueTask DisposeAsync()
        {
            await MainServer.DisposeAsync();
            await IdpServer.DisposeAsync();
        }
    }

    [Fact]
    public async Task no_spaces_host_completes_static_external_login_unchanged()
    {
        await using var fixture = await CreateNoSpacesStaticExternalLoginFixtureAsync(
            nameof(no_spaces_host_completes_static_external_login_unchanged));

        using var mainClient = fixture.MainServer.CreateClient();
        using var idpClient = fixture.IdpServer.CreateClient();

        var challenge = await mainClient.GetAsync("/test/challenge", _ct);
        challenge.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        var authorizeUrl = challenge.Headers.Location!.ToString();

        (await idpClient.GetAsync("/login?sub=alice", _ct)).EnsureSuccessStatusCode();
        var idpAuthorize = await idpClient.GetAsync(authorizeUrl, _ct);
        idpAuthorize.StatusCode.ShouldBe(HttpStatusCode.SeeOther);

        var callback = await mainClient.GetAsync(idpAuthorize.Headers.Location!.ToString(), _ct);
        callback.StatusCode.ShouldBe(HttpStatusCode.Redirect);

        var finish = await mainClient.GetAsync(callback.Headers.Location!.ToString(), _ct);
        finish.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await finish.Content.ReadAsStringAsync(_ct)).ShouldBe("alice");
    }

    private async Task<NoSpacesFixture> CreateNoSpacesStaticExternalLoginFixtureAsync(string name)
    {
        const string oidcScheme = "no-spaces-oidc";
        const string clientId = "no-spaces-client";
        const string clientSecret = "secret";
        var output = TestContext.Current.TestOutputHelper!;

        var mainHostAlias = $"ms{Guid.NewGuid():N}"[..10];
        var idpHostAlias = $"idp{Guid.NewGuid():N}"[..10];

        KestrelBasedTestServer idpServer = null;

        var mainServer = new KestrelBasedTestServer(
            mainHostAlias,
            _webServerFixture,
            new PrefixedTestOutputHelper(output, name + "-main"),
            services =>
            {
                services.AddRouting();

                // Deliberately no services.AddSpaces(): this is the no-spaces regression host.
                services.AddIdentityServer(options => options.KeyManagement.Enabled = false)
                    .AddInMemoryClients([])
                    .AddInMemoryApiScopes([])
                    .AddInMemoryIdentityResources([]);

                services.AddAuthentication()
                    .AddOpenIdConnect(oidcScheme, options =>
                    {
                        options.SignInScheme = IdentityServerConstants.ExternalCookieAuthenticationScheme;
                        options.Authority = idpServer.BaseAddress.ToString().TrimEnd('/');
                        options.ClientId = clientId;
                        options.ClientSecret = clientSecret;
                        options.CallbackPath = $"/signin-{oidcScheme}";
                        options.ResponseType = "code";
                        options.ResponseMode = "query";
                        options.Scope.Clear();
                        options.Scope.Add("openid");
                        options.MapInboundClaims = false;
                        options.SaveTokens = false;
                        options.BackchannelHttpHandler = idpServer.CreateHandler();
                    });
            },
            webapp =>
            {
                // Deliberately no webapp.UseSpaceResolution().
                webapp.UseIdentityServer();

                webapp.Use(async (ctx, next) =>
                {
                    if (ctx.Request.Path.Value == "/test/challenge")
                    {
                        await ctx.ChallengeAsync(oidcScheme,
                            new AuthenticationProperties { RedirectUri = "/test/finish" });
                        return;
                    }

                    if (ctx.Request.Path.Value == "/test/finish")
                    {
                        var external = await ctx.AuthenticateAsync(IdentityServerConstants.ExternalCookieAuthenticationScheme);
                        if (external.Succeeded)
                        {
                            ctx.Response.StatusCode = (int)HttpStatusCode.OK;
                            await ctx.Response.WriteAsync(external.Principal!.FindFirst("sub")!.Value);
                        }
                        else
                        {
                            ctx.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                        }
                        return;
                    }

                    await next(ctx);
                });
            });

        idpServer = new KestrelBasedTestServer(
            idpHostAlias,
            _webServerFixture,
            new PrefixedTestOutputHelper(output, name + "-idp"),
            services =>
            {
                services.AddRouting();
                services.AddAuthorization();

                services.AddIdentityServer(options => options.KeyManagement.Enabled = false)
                    .AddInMemoryClients([
                        new Client
                        {
                            ClientId = clientId,
                            ClientSecrets = { new Secret(clientSecret.Sha256()) },
                            AllowedGrantTypes = GrantTypes.Code,
                            RequireConsent = false,
                            RedirectUris = { mainServer.BuildUrl($"/signin-{oidcScheme}").ToString() },
                            AllowedScopes = { "openid" }
                        }
                    ])
                    .AddInMemoryIdentityResources([new IdentityResources.OpenId()])
                    .AddDeveloperSigningCredential(persistKey: false);
            },
            webapp =>
            {
                webapp.UseIdentityServer();

                webapp.MapGet("/login", async ctx =>
                {
                    var sub = ctx.Request.Query["sub"].FirstOrDefault() ?? "external-user";
                    await ctx.SignInAsync(new IdentityServerUser(sub).CreatePrincipal());
                });
            });

        await idpServer.StartAsync();
        await mainServer.StartAsync();

        return new NoSpacesFixture { MainServer = mainServer, IdpServer = idpServer };
    }
}
