// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Authentication.Cookies;
using Sustainsys.Saml2;
using Sustainsys.Saml2.AspNetCore2;
using Sustainsys.Saml2.Configuration;
using Sustainsys.Saml2.Metadata;

namespace MvcSaml;

internal static class HostingExtensions
{
    public static WebApplication ConfigureServices(this WebApplicationBuilder builder)
    {
        // The IdentityServer base URL is injected by Aspire at runtime via the "is-host" environment variable.
        var idpBaseUrl = builder.Configuration["is-host"]
            ?? throw new InvalidOperationException("is-host configuration is required");

        builder.Services.AddControllersWithViews();

        builder.Services.AddAuthentication(options =>
            {
                options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = Saml2Defaults.Scheme;
            })
            .AddCookie(options =>
            {
                options.Cookie.Name = "mvcsaml";
            })
            .AddSaml2(options =>
            {
                // SP entity ID — must match the EntityId registered in the IdP's SamlServiceProviders config.
                // By convention, Sustainsys uses <base-url>/Saml2 as the entity ID.
                options.SPOptions.EntityId = new EntityId("https://localhost:44350/Saml2");

                // Best practice: require the IdP to sign assertions.
                options.SPOptions.WantAssertionsSigned = true;

                // Best practice: the SP does not sign AuthnRequests for this sample (no SP cert needed).
                // Set to Always and add a ServiceCertificate to enable signed AuthnRequests.
                options.SPOptions.AuthenticateRequestSigningBehavior = SigningBehavior.Never;

                // Load the IdP configuration from the metadata endpoint published by IdentityServer.
                // This automatically picks up signing certificates, endpoints, and capabilities.
                options.IdentityProviders.Add(
                    new IdentityProvider(new EntityId(idpBaseUrl), options.SPOptions)
                    {
                        MetadataLocation = $"{idpBaseUrl}/saml/metadata",
                        LoadMetadata = true
                    });
            });

        builder.Services.AddAuthorization();

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
