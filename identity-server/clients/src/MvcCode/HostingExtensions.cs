// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.IdentityModel.Tokens.Jwt;
using Duende.IdentityModel;
using Duende.IdentityModel.Client;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.IdentityModel.Tokens;

namespace MvcCode;

internal static class HostingExtensions
{
    public static WebApplication ConfigureServices(this WebApplicationBuilder builder)
    {
        // Get the authority from the environment variable set by Aspire
        var authority = builder.Configuration["is-host"];

        JwtSecurityTokenHandler.DefaultMapInboundClaims = false;

        builder.Services.AddControllersWithViews();

        builder.Services.AddHttpClient();

        builder.Services.AddSingleton<IDiscoveryCache>(r =>
        {
            var factory = r.GetRequiredService<IHttpClientFactory>();
            return new DiscoveryCache(authority, () => factory.CreateClient());
        });

        builder.Services.AddAuthentication(options =>
        {
            options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = "oidc";
        })
            .AddCookie(options =>
            {
                options.Cookie.Name = "mvccode";
            })
            .AddOpenIdConnect("oidc", options =>
            {
                options.Authority = authority;

                options.ClientId = "mvc.code";
                options.ClientSecret = "secret";

                // code flow + PKCE (PKCE is turned on by default)
                options.ResponseType = "code";
                options.UsePkce = true;

                options.Scope.Clear();
                options.Scope.Add("openid");
                options.Scope.Add("profile");
                options.Scope.Add("email");
                options.Scope.Add("custom.profile");
                options.Scope.Add("resource1.scope1");
                options.Scope.Add("resource2.scope1");
                options.Scope.Add("offline_access");

                // not mapped by default
                options.ClaimActions.MapAll();
                options.ClaimActions.MapJsonKey("website", "website");
                options.ClaimActions.MapCustomJson("address", (json) => json.GetRawText());

                options.GetClaimsFromUserInfoEndpoint = true;
                options.SaveTokens = true;

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    NameClaimType = JwtClaimTypes.Name,
                    RoleClaimType = JwtClaimTypes.Role,
                };

                options.DisableTelemetry = true;
            });

        // Register a named HttpClient with service discovery support.
        // The AddServiceDiscovery extension enables Aspire to resolve the actual endpoint at runtime.
        builder.Services.AddHttpClient("SimpleApi", client =>
        {
            client.BaseAddress = new Uri("https://simple-api");
        })
        .AddServiceDiscovery();

        return builder.Build();
    }

    public static WebApplication ConfigurePipeline(this WebApplication app)
    {
        app.UseDeveloperExceptionPage();
        app.UseHttpsRedirection();
        app.UseStaticFiles();

        app.UseRouting();

        app.UseAuthentication();
        app.UseAuthorization();

        app.MapDefaultControllerRoute()
            .RequireAuthorization();

        return app;
    }
}
