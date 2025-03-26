// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using System.Text.Json;
using Bff.Blazor.UnitTests.Components;
using Duende.Bff;
using Duende.Bff.Blazor;
using Duende.Bff.Tests.TestFramework;
using Duende.Bff.Tests.TestHosts;
using Duende.Bff.Yarp;
using Yarp.ReverseProxy.Forwarder;
using Yarp.ReverseProxy.Transforms;
using Yarp.ReverseProxy.Transforms.Builder;

namespace Bff.Blazor.UnitTests;

public class BffBlazorHost : GenericHost
{
    public enum ResponseStatus
    {
        Ok, Challenge, Forbid
    }
    public ResponseStatus LocalApiResponseStatus { get; set; } = ResponseStatus.Ok;

    private readonly IdentityServerHost _identityServerHost;
    private readonly ApiHost _apiHost;
    private readonly string _clientId;
    public BffOptions BffOptions { get; private set; } = null!;

    public BffBlazorHost(
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
        UseForwardedHeaders = useForwardedHeaders;

        OnConfigureServices += ConfigureServices;
        OnConfigure += Configure;
    }

    private void ConfigureServices(IServiceCollection services)
    {
        services.AddRazorComponents()
            .AddInteractiveServerComponents()
            .AddInteractiveWebAssemblyComponents();

        services.AddCascadingAuthenticationState();

        services.AddRouting();
        services.AddAuthorization();

        var bff = services.AddBff(options =>
        {
            BffOptions = options;
        })
            .AddBlazorServer();

        services.AddSingleton<IForwarderHttpClientFactory>(
            new CallbackHttpMessageInvokerFactory(
                () => new HttpMessageInvoker(_apiHost.Server.CreateHandler())));

        services.AddAuthentication("cookie")
            .AddCookie("cookie", options =>
            {
                options.Cookie.Name = "bff";
            });

        services.AddSingleton<BffYarpTransformBuilder>(CustomDefaultBffTransformBuilder);

        bff.AddServerSideSessions();
        bff.AddRemoteApis();

        services.AddAuthentication(options =>
        {
            options.DefaultChallengeScheme = "oidc";
            options.DefaultSignOutScheme = "oidc";
        })
            .AddOpenIdConnect("oidc", options =>
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

        services.AddSingleton<FailureAccessTokenRetriever>();

        services.AddSingleton(new TestAccessTokenRetriever(async ()
            => await _identityServerHost.CreateJwtAccessTokenAsync()));
    }


    private void CustomDefaultBffTransformBuilder(string localpath, TransformBuilderContext context)
    {
        context.AddResponseHeader("added-by-custom-default-transform", "some-value");
        DefaultBffYarpTransformerBuilders.DirectProxyWithAccessToken(localpath, context);
    }

    private void Configure(IApplicationBuilder app)
    {
        app.UseAuthentication();

        app.UseRouting();

        app.UseBff();
        app.UseAntiforgery();
        app.UseAuthorization();

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapBffManagementEndpoints();
            endpoints.MapRazorComponents<App>()
                .AddInteractiveServerRenderMode()
                .AddInteractiveWebAssemblyRenderMode();
        });


    }

    public async Task<bool> GetIsUserLoggedInAsync(string? userQuery = null)
    {
        if (userQuery != null) userQuery = '?' + userQuery;

        var req = new HttpRequestMessage(HttpMethod.Get, Url("/bff/user") + userQuery);
        req.Headers.Add("x-csrf", "1");
        var response = await BrowserClient.SendAsync(req);

        (response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.Unauthorized)
            .ShouldBeTrue();

        return response.StatusCode == HttpStatusCode.OK;
    }

    public async Task<List<JsonRecord>> CallUserEndpointAsync()
    {
        var req = new HttpRequestMessage(HttpMethod.Get, Url("/bff/user"));
        req.Headers.Add("x-csrf", "1");

        var response = await BrowserClient.SendAsync(req);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/json");

        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<List<JsonRecord>>(json, TestSerializerOptions.Default) ?? [];
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
        response.Headers.Location!.ToString().ToLowerInvariant().ShouldStartWith(_identityServerHost.Url("/connect/endsession"));

        response = await _identityServerHost.BrowserClient.GetAsync(response.Headers.Location.ToString());
        response.StatusCode.ShouldBe(HttpStatusCode.Redirect); // logout
        response.Headers.Location!.ToString().ToLowerInvariant().ShouldStartWith(_identityServerHost.Url("/account/logout"));

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

    public class CallbackHttpMessageInvokerFactory : IForwarderHttpClientFactory
    {
        public CallbackHttpMessageInvokerFactory(Func<HttpMessageInvoker> callback)
        {
            CreateInvoker = callback;
        }

        public Func<HttpMessageInvoker> CreateInvoker { get; set; }

        public HttpMessageInvoker CreateClient(ForwarderHttpClientContext context)
        {
            return CreateInvoker.Invoke();
        }
    }
}
