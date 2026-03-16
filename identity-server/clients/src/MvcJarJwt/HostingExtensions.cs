// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.AccessTokenManagement;
using Duende.AccessTokenManagement.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace MvcJarJwt;

internal static class HostingExtensions
{
    public static WebApplication ConfigureServices(this WebApplicationBuilder builder)
    {
        var authority = builder.Configuration["is-host"];
        var simpleApi = builder.Configuration["simple-api"];

        _ = builder.Services.AddSingleton<AssertionService>();
        _ = builder.Services.AddTransient<OidcEvents>();

        // add MVC
        _ = builder.Services.AddControllersWithViews();

        // add cookie-based session management with OpenID Connect authentication
        _ = builder.Services.AddAuthentication(options =>
        {
            options.DefaultScheme = "cookie";
            options.DefaultChallengeScheme = "oidc";
        })
            .AddCookie("cookie", options =>
            {
                options.Cookie.Name = "mvcjarjwt";

                options.ExpireTimeSpan = TimeSpan.FromHours(8);
                options.SlidingExpiration = false;

                options.Events.OnSigningOut = async e =>
                {
                    // automatically revoke refresh token at signout time
                    await e.HttpContext.RevokeRefreshTokenAsync();
                };
            })
            .AddOpenIdConnect("oidc", options =>
            {
                options.Authority = authority;
                options.RequireHttpsMetadata = false;

                options.ClientId = "mvc.jar.jwt";

                // code flow + PKCE (PKCE is turned on by default)
                options.ResponseType = "code";
                options.UsePkce = true;

                options.Scope.Clear();
                options.Scope.Add("openid");
                options.Scope.Add("profile");
                options.Scope.Add("resource1.scope1");
                options.Scope.Add("offline_access");

                // keeps id_token smaller
                options.GetClaimsFromUserInfoEndpoint = true;
                options.SaveTokens = true;
                options.MapInboundClaims = false;

                // needed to add JWR / private_key_jwt support
                options.EventsType = typeof(OidcEvents);

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    NameClaimType = "name",
                    RoleClaimType = "role"
                };

                options.DisableTelemetry = true;
            });

        // add automatic token management
        _ = builder.Services.AddOpenIdConnectAccessTokenManagement();
        _ = builder.Services.AddTransient<IClientAssertionService, ClientAssertionService>();

        // add HTTP client to call protected API
        _ = builder.Services.AddUserAccessTokenHttpClient("client", configureClient: client =>
        {
            client.BaseAddress = new Uri("https://simple-api");
        }).AddServiceDiscovery();

        return builder.Build();
    }

    public static WebApplication ConfigurePipeline(this WebApplication app)
    {
        _ = app.UseDeveloperExceptionPage();
        _ = app.UseHttpsRedirection();
        _ = app.UseStaticFiles();

        _ = app.UseRouting();

        _ = app.UseAuthentication();
        _ = app.UseAuthorization();

        _ = app.MapDefaultControllerRoute()
            .RequireAuthorization();

        return app;
    }
}
