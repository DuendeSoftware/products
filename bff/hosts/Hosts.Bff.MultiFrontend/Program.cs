// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Claims;
using System.Text.Json;
using Bff;
using Duende.AccessTokenManagement.OpenIdConnect;
using Duende.Bff;
using Duende.Bff.AccessTokenManagement;
using Duende.Bff.Configuration;
using Duende.Bff.DynamicFrontends;
using Duende.Bff.Yarp;
using Hosts.ServiceDefaults;
using Yarp.ReverseProxy.Configuration;

var bffConfig = new ConfigurationBuilder()
#if DEBUG
    .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "BffConfig.json"), optional: false, reloadOnChange: true)
#else
    .AddJsonFile("BffConfig.json", optional: false, reloadOnChange: true)
#endif
    .Build();

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<IIndexHtmlClient, CustomIndexHtmlClient>();

builder.Services.AddUserAccessTokenHttpClient("api",
    configureClient: client =>
    {
        client.BaseAddress = new Uri("https://localhost:5011/api");
    });


builder.AddServiceDefaults();

var bffBuilder = builder.Services
    .AddBff();

bffBuilder
    .WithDefaultOpenIdConnectOptions(options =>
    {
        var authority = ServiceDiscovery.ResolveService(AppHostServices.IdentityServer);
        options.Authority = authority.ToString();

        // confidential client using code flow + PKCE
        //options.ClientId = "bff.multi-frontend.default";
        //options.ClientSecret = "secret";
        options.ResponseType = "code";
        options.ResponseMode = "query";

        options.MapInboundClaims = false;
        options.GetClaimsFromUserInfoEndpoint = true;
        options.SaveTokens = true;

        // request scopes + refresh tokens
        options.Scope.Clear();
        options.Scope.Add("openid");
        options.Scope.Add("profile");
        options.Scope.Add("api");
        options.Scope.Add("scope-for-isolated-api");
        options.Scope.Add("offline_access");

        options.Resource = "urn:isolated-api";


    })
    .WithDefaultCookieOptions(options =>
    {
        //options.Cookie.Name = "bff.multi-frontend.default";
        //options.Cookie.SameSite = SameSiteMode.None;
        //options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        //options.Cookie.Path = "/bff";
        //options.Cookie.HttpOnly = true;
        //options.LoginPath = "/bff/login";
        //options.LogoutPath = "/bff/logout";
    })
    .LoadConfiguration(bffConfig)
    .AddRemoteApis()
    .AddFrontends(
        new BffFrontend(BffFrontendName.Parse("default-frontend"))
            .WithIndexHtmlUrl(new Uri("https://localhost:5005/static/index.html"))
,
        new BffFrontend(BffFrontendName.Parse("with-path"))
            .WithOpenIdConnectOptions(opt =>
            {
                opt.ClientId = "bff.multi-frontend.with-path";
                opt.ClientSecret = "secret";
            })
            .WithIndexHtmlUrl(new Uri("https://localhost:5005/static/index.html"))
            .MappedToPath(LocalPath.Parse("/with-path")),

        new BffFrontend(BffFrontendName.Parse("with-domain"))
                .WithOpenIdConnectOptions(opt =>
                {
                    opt.ClientId = "bff.multi-frontend.with-domain";
                    opt.ClientSecret = "secret";
                })
                .WithIndexHtmlUrl(new Uri("https://localhost:5005/static/index.html"))
                .MappedToOrigin(Origin.Parse("https://app1.localhost:5005"))
                .WithRemoteApis(
                    new RemoteApi(LocalPath.Parse("/api/user-token"), new Uri("https://localhost:5010")),
                    new RemoteApi(LocalPath.Parse("/api/client-token"), new Uri("https://localhost:5010"))
                        .WithAccessToken(RequiredTokenType.Client),
                    new RemoteApi(LocalPath.Parse("/api/user-or-client-token"), new Uri("https://localhost:5010"))
                        .WithAccessToken(RequiredTokenType.UserOrClient),
                    new RemoteApi(LocalPath.Parse("/api/anonymous"), new Uri("https://localhost:5010"))
                        .WithAccessToken(RequiredTokenType.None),
                    new RemoteApi(LocalPath.Parse("/api/optional-user-token"), new Uri("https://localhost:5010"))
                        .WithAccessToken(RequiredTokenType.UserOrNone),
                    new RemoteApi(LocalPath.Parse("/api/impersonation"), new Uri("https://localhost:5010"))
                        .WithAccessTokenRetriever<ImpersonationAccessTokenRetriever>(),
                    new RemoteApi(LocalPath.Parse("/api/audience-constrained"), new Uri("https://localhost:5010"))
                        .WithUserAccessTokenParameters(new BffUserAccessTokenParameters { Resource = Resource.Parse("urn:isolated-api") }))
        )
    .AddYarpConfig(BuildYarpRoutes(), [
        new ClusterConfig()
        {

            ClusterId = "cluster1",

            Destinations = new Dictionary<string, DestinationConfig>(StringComparer.OrdinalIgnoreCase)
            {
                { "destination1", new() { Address = "https://localhost:5010" } },
            }
        }
    ]);



var app = builder.Build();

app.UseHttpsRedirection();

app.UseAuthentication();

app.UseRouting();


app.UseBff();


app.Map("/static", staticApp =>
{
    staticApp.UseDefaultFiles();

    staticApp.UseStaticFiles(new StaticFileOptions()
    {

    });

});


app.MapGet("/local/self-contained", (CurrentFrontendAccessor currentFrontendAccessor, ClaimsPrincipal user) =>
{

    var data = new
    {
        FrontendName = currentFrontendAccessor.Get().Name.ToString(),
        Message = "Hello from self-contained local API",
        User = user!.FindFirst("name")?.Value ?? user!.FindFirst("sub")!.Value
    };

    return data;
});

app.MapGet("/local/invokes-external-api", async (CurrentFrontendAccessor currentFrontendAccessor, IHttpClientFactory httpClientFactory, HttpContext c, CancellationToken ct) =>
{
    var httpClient = httpClientFactory.CreateClient("api");
    var apiResult = await httpClient.GetAsync("/user-token");
    var content = await apiResult.Content.ReadAsStringAsync();
    var deserialized = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(content);

    var data = new
    {
        FrontendName = currentFrontendAccessor.Get().Name.ToString(),
        Message = "Hello from local API that invokes a remote api",
        RemoteApiResponse = deserialized
    };

    return data;
});

app.Run();

RouteConfig[] BuildYarpRoutes()
{
    return [
        new RouteConfig()
        {
            RouteId = "user-token",
            ClusterId = "cluster1",

            Match = new()
            {
                Path = "/yarp/user-token/{**catch-all}"
            }
        }.WithAccessToken(RequiredTokenType.User).WithAntiforgeryCheck(),
        new RouteConfig()
        {
            RouteId = "client-token",
            ClusterId = "cluster1",

            Match = new()
            {
                Path = "/yarp/client-token/{**catch-all}"
            }
        }.WithAccessToken(RequiredTokenType.Client).WithAntiforgeryCheck(),
        new RouteConfig()
        {
            RouteId = "user-or-client-token",
            ClusterId = "cluster1",

            Match = new()
            {
                Path = "/yarp/user-or-client-token/{**catch-all}"
            }
        }.WithAccessToken(RequiredTokenType.UserOrClient).WithAntiforgeryCheck(),
        new RouteConfig()
            {
                RouteId = "anonymous",
                ClusterId = "cluster1",

                Match = new()
                {
                    Path = "/yarp/anonymous/{**catch-all}"
                }
            }
            .WithAntiforgeryCheck()
    ];
}


public class CustomIndexHtmlClient(HttpClient client, CurrentFrontendAccessor currentFrontendAccessor) : IIndexHtmlClient
{
    public async Task<string?> GetIndexHtmlAsync(CancellationToken ct)
    {
        if (!currentFrontendAccessor.TryGet(out var frontend))
        {
            return null;
        }

        var indexHtmlUrl = frontend.IndexHtmlUrl;

        if (indexHtmlUrl is null)
        {
            return null;
        }

        var html = await client.GetStringAsync(indexHtmlUrl, ct);

        html = html.Replace("[FrontendName]", frontend.Name);
        html = html.Replace("[Path]", frontend.SelectionCriteria.MatchingPath + "/"); // Note, the path must end with a slash


        return html;
    }
}
