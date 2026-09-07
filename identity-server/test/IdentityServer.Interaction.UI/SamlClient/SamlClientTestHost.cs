// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Cryptography.X509Certificates;
using Duende.IdentityServer.Interaction.SharedHosts.IdentityServer;
using Duende.IdentityServer.UI.Infra;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Sustainsys.Saml2.AspNetCore2;
using IdentityProvider = Sustainsys.Saml2.IdentityProvider;

namespace Duende.IdentityServer.Interaction.SharedHosts.SamlClient;

public sealed class SamlClientTestHost : TestHost
{
    private const string DefaultName = "saml-client";

    private readonly IdentityServerTestHost _identityServer;
    private readonly X509Certificate2? _signingCertificate;

    // The entity ID is a logical identifier — it uses a stable URL without a port
    // so it can be established before the server starts. The actual ACS URL (with
    // dynamic port) is set separately in the SamlServiceProvider registration after startup.
    public string EntityId => $"https://{Name}.dev.localhost/Saml2";

    public SamlClientTestHost(
        IScenarioConfigurator configurator,
        IdentityServerTestHost identityServer)
        : this(configurator, identityServer, signingCertificate: null)
    {
    }

    public SamlClientTestHost(
        IScenarioConfigurator configurator,
        IdentityServerTestHost identityServer,
        X509Certificate2? signingCertificate)
        : this(configurator, identityServer, signingCertificate, DefaultName)
    {
    }

    public SamlClientTestHost(
        IScenarioConfigurator configurator,
        IdentityServerTestHost identityServer,
        X509Certificate2? signingCertificate,
        string name)
        : base(configurator, name)
    {
        _identityServer = identityServer;
        _signingCertificate = signingCertificate;
    }

    protected override WebApplication CreateApp(WebApplicationBuilder builder)
    {
        builder.ServeEmbeddedUi("SamlClient");

        builder.Services.AddRazorPages()
            .WithRazorPagesRoot("/SamlClient/Pages")
            .AddApplicationPart(typeof(SamlClientTestHost).Assembly);

        var idpBaseUri = _identityServer.BuildUri().ToString().TrimEnd('/');
        var entityId = EntityId;

        builder.Services.AddAuthentication(options =>
        {
            options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = Saml2Defaults.Scheme;
        })
        .AddCookie(options =>
        {
            options.Cookie.Name = Name;
        })
        .AddSaml2(options =>
        {
            options.SPOptions.EntityId = new Sustainsys.Saml2.Metadata.EntityId(entityId);

            if (_signingCertificate != null)
            {
                options.SPOptions.ServiceCertificates.Add(_signingCertificate);
            }

            options.IdentityProviders.Add(
                new IdentityProvider(
                    new Sustainsys.Saml2.Metadata.EntityId($"{idpBaseUri}/Saml2"),
                    options.SPOptions)
                {
                    LoadMetadata = true,
                    MetadataLocation = $"{idpBaseUri}/Saml2",
                    SingleSignOnServiceUrl = new Uri($"{idpBaseUri}/Saml2/SSO"),
                    WantAuthnRequestsSigned = _signingCertificate != null
                });
        });

        builder.Services.AddAuthorization();

        var app = builder.Build();

        app.UseStaticFiles();
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapRazorPages();

        return app;
    }
}
