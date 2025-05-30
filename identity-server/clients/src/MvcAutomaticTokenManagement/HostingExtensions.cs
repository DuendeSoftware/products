// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authentication;
using Microsoft.IdentityModel.Tokens;

namespace MvcAutomaticTokenManagement;

internal static class HostingExtensions
{
    public static WebApplication ConfigureServices(this WebApplicationBuilder builder)
    {
        JwtSecurityTokenHandler.DefaultMapInboundClaims = false;

        var authority = builder.Configuration["is-host"];

        // add MVC
        builder.Services.AddControllersWithViews();

        // add cookie-based session management with OpenID Connect authentication
        builder.Services.AddAuthentication(options =>
        {
            options.DefaultScheme = "cookie";
            options.DefaultChallengeScheme = "oidc";
        })
            .AddCookie("cookie", options =>
            {
                options.Cookie.Name = "mvcclient";

                options.ExpireTimeSpan = TimeSpan.FromHours(8);
                options.SlidingExpiration = false;

                // could be used to automatically trigger re-authentication (if you want to do that at the pipeline level)
                //options.Events.OnValidatePrincipal = async e =>
                //{
                //    var currentToken = await e.HttpContext.GetAccessTokenAsync();

                //    if (string.IsNullOrWhiteSpace(currentToken))
                //    {
                //        e.RejectPrincipal();
                //    }
                //};

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

                options.ClientId = "mvc.tokenmanagement";
                options.ClientSecret = "secret";

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

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    NameClaimType = "name",
                    RoleClaimType = "role"
                };

                options.DisableTelemetry = true;
            });

        // add automatic token management
        builder.Services.AddOpenIdConnectAccessTokenManagement();

        // add HTTP client to call protected API
        builder.Services.AddUserAccessTokenHttpClient("client", configureClient: client =>
        {
            client.BaseAddress = new Uri("https://simple-api");
        }).AddServiceDiscovery();

        // var apiKey = _configuration["HoneyCombApiKey"];
        // var dataset = "IdentityServerDev";
        //
        // services.AddOpenTelemetryTracing(builder =>
        // {
        //     builder
        //         //.AddConsoleExporter()
        //         .SetResourceBuilder(
        //             ResourceBuilder.CreateDefault()
        //                 .AddService("MVC TokenManagement"))
        //         //.SetSampler(new AlwaysOnSampler())
        //         .AddHttpClientInstrumentation()
        //         .AddAspNetCoreInstrumentation()
        //         .AddSqlClientInstrumentation()
        //         .AddOtlpExporter(option =>
        //         {
        //             option.Endpoint = new Uri("https://api.honeycomb.io");
        //             option.Headers = $"x-honeycomb-team={apiKey},x-honeycomb-dataset={dataset}";
        //         });
        // });

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
