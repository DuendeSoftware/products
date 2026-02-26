// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Authentication.Cookies;
using Sustainsys.Saml2;
using Sustainsys.Saml2.AspNetCore2;
using Sustainsys.Saml2.Configuration;
using Sustainsys.Saml2.Metadata;

namespace MvcSaml;

internal static class HostingExtensions
{
    // The SP certificate is used to sign AuthnRequests and LogoutRequests sent to the IdP.
    // Generate it with the commands in README.md, then restart both this app and the IdP host.
    // Without the certificate, AuthnRequest signing and SP-initiated single logout are unavailable.
    private const string SpCertificatePath = "saml-sp.pfx";
    private const string SpCertificatePassword = "changeit";

    public static WebApplication ConfigureServices(this WebApplicationBuilder builder)
    {
        // The IdentityServer base URL is injected by Aspire at runtime via the "is-host" environment variable.
        var idpBaseUrl = builder.Configuration["is-host"]
            ?? throw new InvalidOperationException("is-host configuration is required");

        builder.Services.AddControllersWithViews();

        var spCert = LoadSpCertificate();

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

                if (spCert != null)
                {
                    // Best practice: sign AuthnRequests and LogoutRequests with the SP's certificate.
                    // The IdP validates the signature using the public key registered in SamlServiceProviders.
                    options.SPOptions.ServiceCertificates.Add(spCert);
                    options.SPOptions.AuthenticateRequestSigningBehavior = SigningBehavior.Always;
                }
                else
                {
                    // No certificate available — AuthnRequest signing and SP-initiated SLO are unavailable.
                    // See README.md for instructions on generating saml-sp.pfx.
                    options.SPOptions.AuthenticateRequestSigningBehavior = SigningBehavior.Never;
                }

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

    // Returns null if the certificate file has not been generated yet.
    // See README.md for generation instructions.
    private static X509Certificate2 LoadSpCertificate()
    {
        if (!File.Exists(SpCertificatePath))
        {
            return null;
        }

        return X509CertificateLoader.LoadPkcs12FromFile(SpCertificatePath, SpCertificatePassword);
    }
}
