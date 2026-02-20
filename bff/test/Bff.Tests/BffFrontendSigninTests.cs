// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using Duende.Bff.AccessTokenManagement;
using Duende.Bff.Configuration;
using Duende.Bff.DynamicFrontends;
using Duende.Bff.Endpoints;
using Duende.Bff.Tests.TestFramework;
using Duende.Bff.Tests.TestInfra;
using Duende.Bff.Yarp;
using Duende.IdentityServer.Extensions;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
namespace Duende.Bff.Tests;

public class BffFrontendSigninTests : BffTestBase
{
    public BffFrontendSigninTests() : base() =>
        Bff.OnConfigureApp += app =>
        {
            app.MapGet("/secret", (HttpContext c) =>
            {
                if (!c.User.IsAuthenticated())
                {
                    c.Response.StatusCode = 401;
                    return "";
                }

                return "";
            });
        };


    [Fact]
    public async Task Can_get_home()
    {
        await InitializeAsync();

        AddOrUpdateFrontend(Some.BffFrontend());

        await Bff.BrowserClient.GetAsync("/")
            .CheckHttpStatusCode()
            .CheckResponseContent(Bff.DefaultRootResponse);
    }

    [Theory]
    [InlineData(Constants.ManagementEndpoints.Login)]
    [InlineData(Constants.ManagementEndpoints.Logout)]
    [InlineData(Constants.ManagementEndpoints.BackChannelLogout)]
    [InlineData(Constants.ManagementEndpoints.Diagnostics)]
    [InlineData(Constants.ManagementEndpoints.SilentLoginCallback)]
    [InlineData(Constants.ManagementEndpoints.User)]
#pragma warning disable CS0618 // Type or member is obsolete
    [InlineData(Constants.ManagementEndpoints.SilentLogin)]
#pragma warning restore CS0618 // Type or member is obsolete
    public async Task Can_hijack_management_endpoints(string endpoint)
    {
        var pathString = "/bff" + endpoint;

        Bff.OnConfigureApp += app =>
        {
            app.MapGet(pathString, (HttpContext c, CT ct) => "ok");
        };

        await InitializeAsync();

        await Bff.BrowserClient.GetAsync(pathString)
            .CheckHttpStatusCode()
            .CheckResponseContent("ok");
    }

    [Fact]
    public async Task Can_hijack_login_and_logout_endpoints_and_call_default()
    {
        var loginCalled = false;
        var logoutCalled = false;
        Bff.OnConfigureApp += app =>
        {
            app.MapGet(Bff.BffOptions.LoginPath, c =>
            {
                loginCalled = true;
                return c.RequestServices.GetRequiredService<ILoginEndpoint>().ProcessRequestAsync(c);
            });

            app.MapGet(Bff.BffOptions.LogoutPath, c =>
            {
                logoutCalled = true;
                return c.RequestServices.GetRequiredService<ILogoutEndpoint>().ProcessRequestAsync(c);
            });
        };
        await InitializeAsync();

        AddOrUpdateFrontend(Some.BffFrontend());

        await Bff.BrowserClient.Login()
            .CheckResponseContent(Bff.DefaultRootResponse);

        await Bff.BrowserClient.Logout();
        loginCalled.ShouldBeTrue();
        logoutCalled.ShouldBeTrue();
    }

    [Fact]
    public async Task cannot_access_secret_page_without_logging_in()
    {
        await InitializeAsync();

        AddOrUpdateFrontend(Some.BffFrontend());

        await Bff.BrowserClient.GetAsync("/secret")
            .CheckHttpStatusCode(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Can_add_frontends_using_AddFrontends_ExtensionMethod()
    {
        IdentityServer.AddClientFor(Some.BffFrontend(), [Bff.Url()]);
        Bff.OnConfigureBff += bff => bff.AddFrontends(Some.BffFrontend());

        await InitializeAsync();

        await Bff.BrowserClient.Login()
            .CheckResponseContent(Bff.DefaultRootResponse);

        await Bff.BrowserClient.GetAsync("/secret")
            .CheckHttpStatusCode();
    }

    [Fact]
    public async Task Can_login_perf()
    {
        await InitializeAsync();

        var frontEnd = (Some.BffFrontend() with
        {
            Name = BffFrontendName.Parse("with_somepath"),
            MatchingCriteria = new FrontendMatchingCriteria()
            {
                MatchingPath = "/somepath"
            },
        });

        IdentityServer.AddClientFor(frontEnd, [Bff.Url()]);

        Bff.AddOrUpdateFrontend(frontEnd);
        for (var i = 0; i < 10; i++)
        {
            var client = Internet.BuildHttpClient(Bff.Url());
            await client.GetAsync("/somepath/bff/login")
                .CheckResponseContent(Bff.DefaultRootResponse);
        }
    }

    [Fact]
    public async Task can_signin_with_path_based_frontend()
    {
        await InitializeAsync();

        var frontEnd = (Some.BffFrontend() with
        {
            Name = BffFrontendName.Parse("with_somepath"),
            MatchingCriteria = new FrontendMatchingCriteria()
            {
                MatchingPath = "/somepath"
            },
        });

        IdentityServer.AddClientFor(frontEnd, [Bff.Url()]);

        Bff.AddOrUpdateFrontend(frontEnd);

        await Bff.BrowserClient.Login("/somepath")
            .CheckResponseContent(Bff.DefaultRootResponse);

        var cookie = Bff.BrowserClient.Cookies.GetCookies(Bff.Url("/somepath")).FirstOrDefault();
        cookie.ShouldNotBeNull();
        cookie.HttpOnly.ShouldBeTrue();
        cookie.Name.ShouldBe(Constants.Cookies.SecurePrefix + "_" + "with_somepath");
        cookie.Secure.ShouldBeTrue();
        cookie.Path.ShouldBe("/somepath");

        await Bff.BrowserClient.GetAsync("/somepath/secret")
            .CheckHttpStatusCode();
    }

    [Fact]
    public async Task given_path_based_frontend_cannot_login_on_root()
    {
        await InitializeAsync();

        var frontEnd = (Some.BffFrontend() with
        {
            Name = BffFrontendName.Parse("with_somepath"),
            MatchingCriteria = new FrontendMatchingCriteria()
            {
                MatchingPath = "/somepath"
            },
        });

        IdentityServer.AddClientFor(frontEnd, [Bff.Url()]);

        Bff.AddOrUpdateFrontend(frontEnd);

        await Bff.BrowserClient.Login(expectedStatusCode: HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task
        given_path_based_frontend_login_endpoint_should_challenge_and_redirect_to_root_with_custom_prefix()
    {
        Bff.OnConfigureServices += svcs =>
        {
            svcs.Configure<BffOptions>(options => { options.ManagementBasePath = "/custom/bff"; });
        };
        await InitializeAsync();

        var frontEnd = (Some.BffFrontend() with
        {
            Name = BffFrontendName.Parse("with_somepath"),
            MatchingCriteria = new FrontendMatchingCriteria()
            {
                MatchingPath = "/somepath"
            },
        });

        IdentityServer.AddClientFor(frontEnd, [Bff.Url()]);

        Bff.AddOrUpdateFrontend(frontEnd);
        await Bff.BrowserClient.Login(expectedStatusCode: HttpStatusCode.NotFound);


        var response = await Bff.BrowserClient.Login("/somepath/custom");
        response.RequestMessage!.RequestUri.ShouldBe(Bff.Url("/somepath"));
    }

    [Fact]
    public async Task given_path_based_frontend_then_can_perform_silent_signin()
    {
        await InitializeAsync();

        var frontEnd = (Some.BffFrontend() with
        {
            Name = BffFrontendName.Parse("with_somepath"),
            MatchingCriteria = new FrontendMatchingCriteria()
            {
                MatchingPath = "/somepath"
            },
        });

        IdentityServer.AddClientFor(frontEnd, [Bff.Url()]);

        Bff.AddOrUpdateFrontend(frontEnd);

        await Bff.BrowserClient.Login("/somepath")
            .CheckResponseContent(Bff.DefaultRootResponse);

        var response = await Bff.BrowserClient.GetAsync("/somepath/bff/silent-login");

        var message = await response.Content.ReadAsStringAsync();
        message.ShouldContain("source:'bff-silent-login");
        message.ShouldContain("isLoggedIn:true");
    }


    [Fact]
    public async Task Can_login()
    {
        await InitializeAsync();

        AddOrUpdateFrontend(Some.BffFrontend());

        await Bff.BrowserClient.Login()
            .CheckResponseContent(Bff.DefaultRootResponse);

        await Bff.BrowserClient.GetAsync("/secret")
            .CheckHttpStatusCode();

        var cookie = Bff.BrowserClient.Cookies.GetCookies(Bff.Url()).FirstOrDefault();
        cookie.ShouldNotBeNull();
        cookie.HttpOnly.ShouldBeTrue();
        cookie.Name.ShouldBe(Constants.Cookies.HostPrefix + "_" + The.FrontendName);
        cookie.Secure.ShouldBeTrue();
        cookie.Path.ShouldBe("/");
    }

    [Fact]
    public async Task When_updating_frontend_then_subsequent_login_uses_new_openid_connect_settings()
    {
        await InitializeAsync();

        var bffFrontend = Some.BffFrontend();
        AddOrUpdateFrontend(bffFrontend);

        await Bff.BrowserClient.Login()
            .CheckResponseContent(Bff.DefaultRootResponse);

        // Bit weird, but the easiest way to see if the new settings are used is to update
        // it to a wrong value and see if it throws.
        AddOrUpdateFrontend(bffFrontend with
        {
            ConfigureOpenIdConnectOptions = opt =>
            {
                The.DefaultOpenIdConnectConfiguration(opt);
                opt.Authority = "https://clearly_wrong";
            }
        });

        await Bff.BrowserClient.Login()
            .ShouldThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task When_updating_frontend_then_subsequent_api_call_uses_updated_token()
    {
        Bff.OnConfigureServices += services =>
        {
            services.AddSingleton<HybridCache, TestHybridCache>();
        };
        Bff.OnConfigureBff += bff => bff.AddRemoteApis();
        IdentityServer.AddClient("differnet_client_id", Bff.Url());
        await InitializeAsync();

        var bffFrontend = Some.BffFrontend().WithRemoteApis(new RemoteApi("/test", Api.Url()).WithAccessToken(RequiredTokenType.Client));
        AddOrUpdateFrontend(bffFrontend);

        await Bff.BrowserClient.Login()
            .CheckResponseContent(Bff.DefaultRootResponse);

        ApiCallDetails response = await Bff.BrowserClient.CallBffHostApi("/test");

        var cache = (TestHybridCache)Bff.Resolve<HybridCache>();

        AddOrUpdateFrontend(bffFrontend with
        {
            ConfigureOpenIdConnectOptions = opt =>
            {
                The.DefaultOpenIdConnectConfiguration(opt);
                opt.ClientId = "differnet_client_id";
            }
        });

        // Update frontend calls RemoveByTagAsync to clear the cache
        // But it does so on a background thread, so we need to wait for it
        cache.WaitUntilRemoveByTagAsyncCalled(TimeSpan.FromSeconds(1));

        await Bff.BrowserClient.Login()
            .CheckResponseContent(Bff.DefaultRootResponse);

        ApiCallDetails response2 = await Bff.BrowserClient.CallBffHostApi("/test");
        response2.Sub.ShouldBeNullOrEmpty();
        response2.ClientId.ShouldBe("differnet_client_id");
    }

    [Fact]
    public async Task When_updating_frontend_then_subsequent_login_uses_new_cookiesettings()
    {
        await InitializeAsync();

        var bffFrontend = Some.BffFrontend();
        AddOrUpdateFrontend(bffFrontend);

        await Bff.BrowserClient.Login()
            .CheckResponseContent(Bff.DefaultRootResponse);

        Bff.BrowserClient.Cookies.Clear(Bff.Url());

        // Bit weird, but the easiest way to see if the new settings are used is to update
        // it to a wrong value and see if it throws.
        AddOrUpdateFrontend(bffFrontend with
        {
            ConfigureCookieOptions = opt => { opt.Cookie.Name = "my_custom_cookie_name"; }
        });

        await Bff.BrowserClient.Login();

        Bff.BrowserClient.Cookies.GetCookies(Bff.Url())
            .ShouldContain(c => c.Name == "my_custom_cookie_name" && c.HttpOnly && c.Secure && c.Path == "/");
    }

    [Fact]
    public async Task Default_settings_augment_frontend_settings()
    {
        Bff.EnableBackChannelHandler = false;

        Bff.OnConfigureBff += bff =>
        {
            bff.ConfigureOpenIdConnect(options =>
            {
                options.Authority = IdentityServer.Url().ToString();

                options.ClientId = "some_frontend";
                options.ClientSecret = DefaultOidcClient.ClientSecret;
                options.ResponseType = DefaultOidcClient.ResponseType;
                options.ResponseMode = DefaultOidcClient.ResponseMode;

                options.MapInboundClaims = false;
                options.GetClaimsFromUserInfoEndpoint = true;
                options.SaveTokens = true;
                options.BackchannelHttpHandler = Internet;
            });
        };

        await InitializeAsync();

        AddOrUpdateFrontend(new BffFrontend()
        {
            Name = BffFrontendName.Parse("some_frontend")
        });

        await Bff.BrowserClient.Login();

        await Bff.BrowserClient.GetAsync("/secret")
            .CheckHttpStatusCode();
    }

    [Fact]
    public async Task Event_handlers_are_used_from_bff_defaults()
    {
        var onTokenValidatedInvoked = false;

        Bff.EnableBackChannelHandler = false;

        await InitializeAsync();

        Bff.BffOptions.ConfigureOpenIdConnectDefaults = (oidc =>
        {
            oidc.Authority = IdentityServer.Url().ToString();

            oidc.ClientId = "some_frontend";
            oidc.ClientSecret = DefaultOidcClient.ClientSecret;
            oidc.ResponseType = DefaultOidcClient.ResponseType;
            oidc.ResponseMode = DefaultOidcClient.ResponseMode;

            oidc.MapInboundClaims = false;
            oidc.GetClaimsFromUserInfoEndpoint = true;
            oidc.SaveTokens = true;
            oidc.BackchannelHttpHandler = Internet;
            oidc.Events.OnTokenValidated += c =>
            {
                onTokenValidatedInvoked = true;
                return Task.CompletedTask;
            };
        });

        AddOrUpdateFrontend(new BffFrontend()
        {
            Name = BffFrontendName.Parse("some_frontend")
        });

        await Bff.BrowserClient.Login();

        await Bff.BrowserClient.GetAsync("/secret")
            .CheckHttpStatusCode();

        onTokenValidatedInvoked.ShouldBeTrue();
    }

    [Fact]
    public async Task Event_handlers_are_used()
    {
        var onTokenValidatedInvoked = false;

        await InitializeAsync();
        // Add a frontend with a custom event handler
        AddOrUpdateFrontend(Some.BffFrontend() with
        {
            ConfigureOpenIdConnectOptions = opt =>
            {
                The.DefaultOpenIdConnectConfiguration(opt);
                opt.Events.OnTokenValidated += c =>
                {
                    onTokenValidatedInvoked = true;
                    return Task.CompletedTask;
                };
            }
        });

        await Bff.BrowserClient.Login();

        await Bff.BrowserClient.GetAsync("/secret")
            .CheckHttpStatusCode();

        onTokenValidatedInvoked.ShouldBeTrue();
    }

    [Fact]
    public async Task When_creating_new_frontend_old_config_is_not_reused()
    {
        var onTokenValidatedInvoked = false;

        // Add a frontend with a custom event handler
        AddOrUpdateFrontend(Some.BffFrontend() with
        {
            ConfigureOpenIdConnectOptions = opt =>
            {
                The.DefaultOpenIdConnectConfiguration(opt);
                opt.Events.OnTokenValidated += c =>
                {
                    onTokenValidatedInvoked = true;
                    return Task.CompletedTask;
                };
            }
        });

        await InitializeAsync();

        await Bff.BrowserClient.Login();
        onTokenValidatedInvoked.ShouldBeTrue();
        onTokenValidatedInvoked = false;
        AddOrUpdateFrontend(new BffFrontend()
        {
            Name = BffFrontendName.Parse("some_frontend"),
            ConfigureOpenIdConnectOptions = opt => { The.DefaultOpenIdConnectConfiguration(opt); }
        });

        onTokenValidatedInvoked.ShouldBeFalse();
    }

    [Fact]
    public async Task When_only_providing_config_then_can_still_log_in()
    {
        var configuration = BuildValidBffOidcConfig();

        Bff.OnConfigureBff += bff =>
        {
            bff.LoadConfiguration(configuration);
        };
        await InitializeAsync();
        IdentityServer.AddClient(The.ClientId, Bff.Url());
        await Bff.BrowserClient.Login();
    }

    [Fact]
    public async Task When_only_providing_config_then_cannot_login_to_unmatched_frontend()
    {
        var configuration = BuildValidBffOidcConfig();

        Bff.OnConfigureBff += bff =>
        {
            bff.LoadConfiguration(configuration);
        };

        await InitializeAsync();
        Bff.AddOrUpdateFrontend(Some.BffFrontend() with
        {
            MatchingCriteria = new FrontendMatchingCriteria()
            {
                MatchingPath = "/not_matched"
            }
        });
        IdentityServer.AddClient(The.ClientId, Bff.Url());
        await Bff.BrowserClient.Login(expectedStatusCode: HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task When_providing_oidc_config_this_is_used_for_matched_frontend()
    {
        var configuration = BuildValidBffOidcConfig();

        Bff.OnConfigureBff += bff =>
        {
            bff.LoadConfiguration(configuration);
        };

        await InitializeAsync();
        Bff.AddOrUpdateFrontend(Some.BffFrontend() with
        {
            ConfigureOpenIdConnectOptions = null,
            MatchingCriteria = new FrontendMatchingCriteria()
            {
                MatchingPath = The.Path
            }
        });
        IdentityServer.AddClient(The.ClientId, Bff.Url(The.Path + "/"));
        await Bff.BrowserClient.Login(The.Path);
    }

    private IConfiguration BuildValidBffOidcConfig()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddJson(new BffConfiguration()
            {
                DefaultOidcSettings = new OidcConfiguration()
                {
                    ClientId = The.ClientId,
                    ClientSecret = The.ClientSecret,
                    ResponseMode = OpenIdConnectResponseMode.Query,
                    ResponseType = "code",
                    Scope = ["openid", "profile", "offline_access"],
                    Authority = IdentityServer.Url(),
                    GetClaimsFromUserInfoEndpoint = true,
                    SaveTokens = true
                }
            })
            .Build();

        return configuration;
    }
}
