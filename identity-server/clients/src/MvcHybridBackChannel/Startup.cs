// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.IdentityModel.Tokens.Jwt;
using Clients;
using Duende.IdentityModel;
using Duende.IdentityModel.Client;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.IdentityModel.Tokens;

namespace MvcHybrid;

public class Startup
{
    private readonly IConfiguration _configuration;

    public Startup(IConfiguration configuration)
    {
        _configuration = configuration;
        JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();
    }

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddControllersWithViews();
        services.AddHttpClient();

        services.AddSingleton<IDiscoveryCache>(r =>
        {
            var factory = r.GetRequiredService<IHttpClientFactory>();
            return new DiscoveryCache(Constants.Authority, () => factory.CreateClient());
        });

        services.AddTransient<CookieEventHandler>();
        services.AddSingleton<LogoutSessionManager>();

        services.AddAuthentication(options =>
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
                options.Authority = Constants.Authority;
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
    }

    public void Configure(IApplicationBuilder app)
    {
        app.UseDeveloperExceptionPage();
        app.UseStaticFiles();

        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapDefaultControllerRoute();
        });
    }
}
