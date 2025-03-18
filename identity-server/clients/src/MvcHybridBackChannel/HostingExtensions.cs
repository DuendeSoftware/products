// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.IdentityModel.Tokens.Jwt;
using Duende.IdentityModel;
using Duende.IdentityModel.Client;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.IdentityModel.Tokens;
using MvcHybrid;

namespace MvcHybridBackChannel;

internal static class HostingExtensions
{
    public static WebApplication ConfigureServices(this WebApplicationBuilder builder)
    {
        JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();

        var authority = builder.Configuration["is-host"];

        builder.Services.AddControllersWithViews();
        builder.Services.AddHttpClient();

        builder.Services.AddSingleton<IDiscoveryCache>(r =>
        {
            var factory = r.GetRequiredService<IHttpClientFactory>();
            return new DiscoveryCache(authority, () => factory.CreateClient());
        });

        builder.Services.AddTransient<CookieEventHandler>();
        builder.Services.AddSingleton<LogoutSessionManager>();

        builder.Services.AddAuthentication(options =>
        {
            options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = "oidc";
        })
            .AddCookie(options =>
            {
                options.ExpireTimeSpan = TimeSpan.FromMinutes(60);
                options.Cookie.Name = "mvchybridbc";

                options.EventsType = typeof(CookieEventHandler);
            })
            .AddOpenIdConnect("oidc", options =>
            {
                options.Authority = authority;
                options.RequireHttpsMetadata = false;

                options.ClientSecret = "secret";
                options.ClientId = "mvc.hybrid.backchannel";

                options.ResponseType = "code id_token";

                options.Scope.Clear();
                options.Scope.Add("openid");
                options.Scope.Add("profile");
                options.Scope.Add("email");
                options.Scope.Add("resource1.scope1");
                options.Scope.Add("offline_access");

                options.ClaimActions.MapAllExcept("iss", "nbf", "exp", "aud", "nonce", "iat", "c_hash");

                options.GetClaimsFromUserInfoEndpoint = true;
                options.SaveTokens = true;

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    NameClaimType = JwtClaimTypes.Name,
                    RoleClaimType = JwtClaimTypes.Role,
                };

                options.DisableTelemetry = true;
            });

        // var apiKey = _configuration["HoneyCombApiKey"];
        // var dataset = "IdentityServerDev";

        // services.AddOpenTelemetryTracing(builder =>
        // {
        //     builder
        //         //.AddConsoleExporter()
        //         .SetResourceBuilder(
        //             ResourceBuilder.CreateDefault()
        //                 .AddService("MVC Hybrid Backchannnel"))
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
        app.UseStaticFiles();

        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapDefaultControllerRoute();

        return app;
    }
}
