// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using Duende.Bff.AccessTokenManagement;
using Duende.Bff.Configuration;
using Duende.Bff.DynamicFrontends;
using Duende.Bff.Tests.TestFramework;
using Duende.Bff.Tests.TestInfra;
using Duende.Bff.Yarp;
using Duende.IdentityServer.Extensions;
using Microsoft.Extensions.Caching.Hybrid;
using Xunit.Abstractions;

namespace Duende.Bff.Tests;

public class BffFrontendSigninTests : BffTestBase
{
    public BffFrontendSigninTests(ITestOutputHelper output) : base(output) =>
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
    public async Task can_signin_with_path_based_frontend()
    {
        await InitializeAsync();

        var frontEnd = (Some.BffFrontend() with
        {
            Name = BffFrontendName.Parse("with_somepath"),
            SelectionCriteria = new FrontendSelectionCriteria()
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
            SelectionCriteria = new FrontendSelectionCriteria()
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
            SelectionCriteria = new FrontendSelectionCriteria()
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
            SelectionCriteria = new FrontendSelectionCriteria()
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

    private class NoOpHybridCache : HybridCache
    {
        public override ValueTask<T> GetOrCreateAsync<TState, T>(string key, TState state,
            Func<TState, CancellationToken, ValueTask<T>> factory, HybridCacheEntryOptions? options = null,
            IEnumerable<string>? tags = null, CancellationToken cancellationToken = new CancellationToken()) => factory(state, cancellationToken);

        public override ValueTask SetAsync<T>(string key, T value, HybridCacheEntryOptions? options = null,
            IEnumerable<string>? tags = null,
            CancellationToken cancellationToken = new CancellationToken()) => ValueTask.CompletedTask;

        public override ValueTask
            RemoveAsync(string key, CancellationToken cancellationToken = new CancellationToken()) =>
            ValueTask.CompletedTask;

        ManualResetEventSlim _waitUntilRemoveByTagAsyncCalled = new ManualResetEventSlim();

        public override ValueTask RemoveByTagAsync(string tag,
            CancellationToken cancellationToken = new CancellationToken())
        {
            _waitUntilRemoveByTagAsyncCalled.Set();
            return new ValueTask();
        }

        public void WaitUntilRemoveByTagAsyncCalled(TimeSpan until)
        {
            _waitUntilRemoveByTagAsyncCalled.Wait(until);
            if (!_waitUntilRemoveByTagAsyncCalled.IsSet)
            {
                throw new TimeoutException();
            }
        }

    }

    [Fact]
    public async Task When_updating_frontend_then_subsequent_api_call_uses_updated_token()
    {
        Bff.OnConfigureServices += services =>
        {
            services.AddSingleton<HybridCache, NoOpHybridCache>();
        };
        Bff.OnConfigureBff += bff => bff.AddRemoteApis();
        IdentityServer.AddClient("differnet_client_id", Bff.Url());
        await InitializeAsync();

        var bffFrontend = Some.BffFrontend().WithRemoteApis(new RemoteApi(LocalPath.Parse("/test"), Api.Url()).WithAccessToken(RequiredTokenType.Client));
        AddOrUpdateFrontend(bffFrontend);

        await Bff.BrowserClient.Login()
            .CheckResponseContent(Bff.DefaultRootResponse);

        ApiCallDetails response = await Bff.BrowserClient.CallBffHostApi("/test");

        var cache = (NoOpHybridCache)Bff.Resolve<HybridCache>();

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
}
