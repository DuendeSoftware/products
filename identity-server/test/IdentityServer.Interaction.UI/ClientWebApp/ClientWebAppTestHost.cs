// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityModel;
using Duende.IdentityModel.Client;
using Duende.IdentityServer.Interaction.SharedHosts.Api;
using Duende.IdentityServer.Interaction.SharedHosts.IdentityServer;
using Duende.IdentityServer.UI.Infra;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.IdentityModel.Tokens;

namespace Duende.IdentityServer.Interaction.SharedHosts.MvcClient;

public class ClientWebAppTestHost(
    IScenarioConfigurator configurator,
    IdentityServerTestHost identityServer,
    ApiHost apiHost,
    string name = "webapp",
    Action<OpenIdConnectOptions>? configureOpenIdConnect = null,
    Action<CookieAuthenticationOptions>? configureCookie = null,
    Action<IServiceCollection>? configureServices = null,
    Action<WebApplication>? configureApp = null) : TestHost(configurator, name)
{
    protected override WebApplication CreateApp(WebApplicationBuilder builder)
    {
        var services = builder.Services;

        configureServices?.Invoke(services);

        services.AddRazorPages()
            .WithRazorPagesRoot("/ClientWebApp/Pages")
            .AddApplicationPart(typeof(ClientWebAppTestHost).Assembly);

        services.AddHttpClient();
        builder.ServeEmbeddedUi("ClientWebApp");

        services.AddSingleton<IDiscoveryCache>(sp =>
        {
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            return new DiscoveryCache(identityServer.BuildUri().ToString(), () => factory.CreateClient());
        });

        services.AddAuthentication(options =>
        {
            options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = "oidc";
        })
    .AddCookie(options =>
    {
        options.Cookie.Name = Name;
        configureCookie?.Invoke(options);
    })
    .AddOpenIdConnect("oidc", options =>
    {
        options.Authority = identityServer.BuildUri().ToString();

        options.ClientId = Name;
        options.ClientSecret = "secret";

        options.ClaimActions.MapAll();
        options.ClaimActions.MapJsonKey("website", "website");
        options.ClaimActions.MapCustomJson("address", json => json.GetRawText());

        options.GetClaimsFromUserInfoEndpoint = true;
        options.SaveTokens = true;

        // code flow + PKCE
        options.ResponseType = "code";
        options.UsePkce = true;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            NameClaimType = JwtClaimTypes.Name,
            RoleClaimType = JwtClaimTypes.Role,
        };

        options.DisableTelemetry = true;

        configureOpenIdConnect?.Invoke(options);
    });

        services.AddHttpClient("api", client =>
        {
            client.BaseAddress = apiHost.BuildUri();
        });

        var app = builder.Build();

        app.UseStaticFiles();
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();



        app.MapRazorPages()
            .RequireAuthorization();

        configureApp?.Invoke(app);

        return app;
    }
}
