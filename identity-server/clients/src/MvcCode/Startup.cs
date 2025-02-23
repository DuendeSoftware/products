using System;
using Clients;
using Duende.IdentityModel;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http;
using Duende.IdentityModel.Client;
using Microsoft.Extensions.Configuration;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using System.Threading.Tasks;

namespace MvcCode;

public class Startup
{
    private readonly IConfiguration _configuration;

    public Startup(IConfiguration configuration)
    {
        _configuration = configuration;
    }
    
    public void ConfigureServices(IServiceCollection services)
    {
        JwtSecurityTokenHandler.DefaultMapInboundClaims = false;

        services.AddControllersWithViews();
        
        services.AddHttpClient();

        services.AddSingleton<IDiscoveryCache>(r =>
        {
            var factory = r.GetRequiredService<IHttpClientFactory>();
            return new DiscoveryCache(Constants.Authority, () => factory.CreateClient());
        });

        services.AddAuthentication(options =>
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
                options.Authority = Constants.Authority;

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
        
        // var apiKey = _configuration["HoneyCombApiKey"];
        // var dataset = "IdentityServerDev";
        //
        // services.AddOpenTelemetryTracing(builder =>
        // {
        //     builder
        //         //.AddConsoleExporter()
        //         .SetResourceBuilder(
        //             ResourceBuilder.CreateDefault()
        //                 .AddService("MVC Code"))
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
        app.UseHttpsRedirection();
        app.UseStaticFiles();

        app.UseRouting();

        app.UseAuthentication();
        app.UseAuthorization();

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapDefaultControllerRoute()
                .RequireAuthorization();
        });
    }
}