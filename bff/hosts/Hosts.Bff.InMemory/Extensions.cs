// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.AccessTokenManagement.OpenIdConnect;
using Duende.Bff;
using Duende.Bff.AccessTokenManagement;
using Duende.Bff.Configuration;
using Duende.Bff.Yarp;
using Hosts.ServiceDefaults;
using Microsoft.Extensions.ServiceDiscovery;

namespace Bff;

internal static class Extensions
{
    public static WebApplication ConfigureServices(
        this WebApplicationBuilder builder,

        // The serviceprovider is needed to do service discovery
        Func<IServiceProvider> getServiceProvider
    )
    {
        var services = builder.Services;

        // Add BFF services to DI - also add server-side session management
        services.AddBff()
            .AddRemoteApis()
            .AddServerSideSessions();

        // local APIs
        services.AddControllers();

        // cookie options
        services.AddAuthentication(options =>
            {
                options.DefaultScheme = "cookie";
                options.DefaultChallengeScheme = "oidc";
                options.DefaultSignOutScheme = "oidc";
            })
            .AddCookie("cookie", options =>
            {
                // set session lifetime
                options.ExpireTimeSpan = TimeSpan.FromHours(8);

                // sliding or absolute
                options.SlidingExpiration = false;

                // host prefixed cookie name
                options.Cookie.Name = "__Host-spa6";

                // strict SameSite handling
                options.Cookie.SameSite = SameSiteMode.Strict;
            })
            .AddOpenIdConnect("oidc", options =>
            {
                // Normally, here you simply configure the authority. But here we want to
                // use service discovery, because aspire can change the url's at run-time. 
                // So, it needs to be discovered at runtime. 
                var authority = DiscoverAuthorityByName(getServiceProvider, AppHostServices.IdentityServer);
                options.Authority = authority;

                // confidential client using code flow + PKCE
                options.ClientId = "bff";
                options.ClientSecret = "secret";
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
            });
        services.AddTransient<ImpersonationAccessTokenRetriever>();

        services.AddUserAccessTokenHttpClient("api",
            configureClient: client => { client.BaseAddress = new Uri("https://localhost:5010/api"); });

        return builder.Build();

    }

    private static string DiscoverAuthorityByName(Func<IServiceProvider> getServiceProvider, string serviceName)
    {
        // Use the ServiceEndpointResolver to perform service discovery
        var resolver = getServiceProvider().GetRequiredService<ServiceEndpointResolver>();
        var authorityEndpoint = resolver.GetEndpointsAsync("https://" + serviceName, CancellationToken.None)
            .GetAwaiter().GetResult(); // Right now I have no way to add this async. 
        var authority = authorityEndpoint.Endpoints[0].ToString()!.TrimEnd('/');
        return authority;
    }

    public static WebApplication ConfigurePipeline(this WebApplication app)
    {
        app.UseHttpLogging();
        app.UseDeveloperExceptionPage();

        app.UseDefaultFiles();
        app.UseStaticFiles();

        app.UseAuthentication();
        app.UseRouting();

        // adds antiforgery protection for local APIs
        app.UseBff();

        // adds authorization for local and remote API endpoints
        app.UseAuthorization();

        // local APIs
        app.MapControllers()
            .RequireAuthorization()
            .AsBffApiEndpoint();

        // login, logout, user, backchannel logout...
        app.MapBffManagementEndpoints();

        //////////////////////////////////////
        // proxy app for cross-site APIs
        //////////////////////////////////////

        // On this path, we use a client credentials token
        app.MapRemoteBffApiEndpoint("/api/client-token", new Uri("https://localhost:5010"))
            .WithAccessToken(RequiredTokenType.Client);

        // On this path, we use a user token if logged in, and fall back to a client credentials token if not
        app.MapRemoteBffApiEndpoint("/api/user-or-client-token", new Uri("https://localhost:5010"))
            .WithAccessToken(RequiredTokenType.UserOrClient);

        // On this path, we make anonymous requests
        app.MapRemoteBffApiEndpoint("/api/anonymous", new Uri("https://localhost:5010"));

        // On this path, we use the client token only if the user is logged in
        app.MapRemoteBffApiEndpoint("/api/optional-user-token", new Uri("https://localhost:5010"))
            .WithAccessToken(RequiredTokenType.UserOrNone);

        // On this path, we require the user token
        app.MapRemoteBffApiEndpoint("/api/user-token", new Uri("https://localhost:5010"))
            .WithAccessToken();

        // On this path, we perform token exchange to impersonate a different user
        // before making the api request
        app.MapRemoteBffApiEndpoint("/api/impersonation", new Uri("https://localhost:5010"))
            .WithAccessToken()
            .WithAccessTokenRetriever<ImpersonationAccessTokenRetriever>();

        // On this path, we obtain an audience constrained token and invoke
        // a different api that requires such a token
        app.MapRemoteBffApiEndpoint("/api/audience-constrained", new Uri("https://localhost:5012"))
            .WithAccessToken()
            .WithUserAccessTokenParameter(new BffUserAccessTokenParameters { Resource = Resource.Parse("urn:isolated-api") });

        return app;
    }

}
