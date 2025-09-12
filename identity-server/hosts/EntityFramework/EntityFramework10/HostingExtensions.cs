// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Claims;
using Duende.IdentityServer;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Hosts.Shared.Customization;
using Duende.IdentityServer.UI;
using Duende.IdentityServer.UI.EntityFramework.Pages.Admin.ApiScopes;
using Duende.IdentityServer.UI.EntityFramework.Pages.Admin.IdentityScopes;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.IdentityModel.Tokens;
using Serilog;

namespace IdentityServerHost;

#pragma warning disable IDE0058 // Expression value is never used. Suppressed throughout the whole file, because there would be a lot of discards otherwise.

internal static class HostingExtensions
{
    internal static WebApplication ConfigureServices(this WebApplicationBuilder builder)
    {
        builder.Services.AddRazorPages()
            .AddRazorRuntimeCompilation();

        builder.Services.AddControllers();
        builder.Services.AddHealthChecks()
                    .AddCheck<DiscoveryHealthCheck>("DiscoveryHealthCheck")
                    .AddCheck<DiscoveryKeysHealthCheck>("DiscoveryKeysHealthCheck");

        // cookie policy to deal with temporary browser incompatibilities
        builder.Services.AddSameSiteCookiePolicy();

        builder.ConfigureIdentityServer();
        builder.AddExternalIdentityProviders();

        builder.AddAdminFeatures();

        builder.Services.AddLocalApiAuthentication(principal =>
        {
            principal.Identities.First().AddClaim(new Claim("additional_claim", "additional_value"));

            return Task.FromResult(principal);
        });

        builder.Services.Configure<KestrelServerOptions>(kestrelOptions =>
        {
            kestrelOptions.ConfigureHttpsDefaults(https =>
            {
                https.ClientCertificateMode = ClientCertificateMode.AllowCertificate;
                https.AllowAnyClientCertificate(); // Needed for the "ephemeral" mtls client
            });
        });

        builder.AddIdentityServerUI();

        return builder.Build();
    }

    private static void AddAdminFeatures(this WebApplicationBuilder builder)
    {
        builder.Services.AddAuthorization(options =>
            options.AddPolicy("admin",
                policy => policy.RequireClaim("sub", "1"))
        );

        builder.Services.Configure<RazorPagesOptions>(options =>
            options.Conventions.AuthorizeFolder("/Admin", "admin"));

        builder.Services.AddTransient<Duende.IdentityServer.UI.EntityFramework.Pages.Admin.Clients.ClientRepository>();
        builder.Services.AddTransient<Duende.IdentityServer.UI.EntityFramework.Pages.Portal.ClientRepository>();
        builder.Services.AddTransient<IdentityScopeRepository>();
        builder.Services.AddTransient<ApiScopeRepository>();
    }

    private static void AddExternalIdentityProviders(this WebApplicationBuilder builder)
    {
        // configures the OpenIdConnect handlers to persist the state parameter into the server-side IDistributedCache.
        builder.Services.AddOidcStateDataFormatterCache("aad", "demoidsrv");

        builder.Services.AddAuthentication()
            .AddOpenIdConnect("Google", "Google", options =>
            {
                options.SignInScheme = IdentityServerConstants.ExternalCookieAuthenticationScheme;
                options.ForwardSignOut = IdentityServerConstants.DefaultCookieAuthenticationScheme;

                options.Authority = "https://accounts.google.com/";
                options.ClientId = "708996912208-9m4dkjb5hscn7cjrn5u0r4tbgkbj1fko.apps.googleusercontent.com";

                options.CallbackPath = "/signin-google";
                options.Scope.Add("email");
                options.MapInboundClaims = false;
            })
            .AddOpenIdConnect("demoidsrv", "IdentityServer (Configuration)", options =>
            {
                options.SignInScheme = IdentityServerConstants.ExternalCookieAuthenticationScheme;
                options.SignOutScheme = IdentityServerConstants.SignoutScheme;

                options.Authority = "https://demo.duendesoftware.com";
                options.ClientId = "login";
                options.ResponseType = "id_token";
                options.SaveTokens = true;
                options.CallbackPath = "/signin-idsrv";
                options.SignedOutCallbackPath = "/signout-callback-idsrv";
                options.RemoteSignOutPath = "/signout-idsrv";
                options.MapInboundClaims = false;

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    NameClaimType = "name",
                    RoleClaimType = "role"
                };
            })
            .AddOpenIdConnect("aad", "Azure AD", options =>
            {
                options.SignInScheme = IdentityServerConstants.ExternalCookieAuthenticationScheme;
                options.SignOutScheme = IdentityServerConstants.SignoutScheme;

                options.Authority = "https://login.windows.net/4ca9cb4c-5e5f-4be9-b700-c532992a3705";
                options.ClientId = "96e3c53e-01cb-4244-b658-a42164cb67a9";
                options.ResponseType = "id_token";
                options.CallbackPath = "/signin-aad";
                options.SignedOutCallbackPath = "/signout-callback-aad";
                options.RemoteSignOutPath = "/signout-aad";
                options.MapInboundClaims = false;

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    NameClaimType = "name",
                    RoleClaimType = "role"
                };
            });
    }

    internal static WebApplication ConfigurePipeline(this WebApplication app)
    {
        app.UseSerilogRequestLogging();

        app.UseCookiePolicy();

        app.UseDeveloperExceptionPage();
        app.UseStaticFiles();

        app.UseRouting();
        app.UseIdentityServer();
        app.UseAuthorization();

        // health checks
        app.MapHealthChecks("/health");

        // local API endpoints
        app.MapControllers()
            .RequireAuthorization(IdentityServerConstants.LocalApi.PolicyName);

        // UI
        app.MapRazorPages()
            .RequireAuthorization();

        app.MapDynamicClientRegistration()
            .AllowAnonymous();

        return app;
    }
}
