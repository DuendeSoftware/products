// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Duende.IdentityServer.Interaction.SharedHosts.IdentityServer;
using Duende.IdentityServer.Interaction.SharedHosts.MvcClient;
using Duende.IdentityServer.Interaction.SharedHosts.SamlClient;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.UI.Infra;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.IdentityServer.Interaction.Scenarios.SamlFederatedLogout;

/// <summary>
/// Builds the shared three-tier mixed-protocol federated logout topology used by the
/// SAML federated logout scenarios: an upstream <see cref="IdentityServerTestHost"/>
/// acting as a SAML IdP, a middle <see cref="IdentityServerTestHost"/> acting
/// simultaneously as a SAML SP (to the upstream) and a SAML IdP (to the downstream
/// SP), a downstream <see cref="SamlClientTestHost"/> acting as a SAML SP, and an
/// optional downstream <see cref="ClientWebAppTestHost"/> registered as an OIDC
/// relying party against the middle with front-channel logout.
/// </summary>
/// <remarks>
/// This helper only wires up hosts and their registrations; it does not start or
/// stop scenario links. Callers (scenarios) are responsible for building their own
/// scenario link lists from the returned hosts, and for disposing every host
/// returned here (in reverse order of creation) from their own <c>StopAsync</c>.
/// </remarks>
public static class MixedProtocolLogoutHosts
{
    /// <summary>
    /// The inline script CSP hash rendered by the SAML POST-binding relay page
    /// (<c>form[name='samlPostBindingSubmit']</c>) that the middle/downstream hosts
    /// use to auto-submit a SAML request/response. Shared by the two scenarios
    /// (<see cref="SamlFederatedLogoutFlow"/> and
    /// <see cref="MixedProtocolLogoutFlow"/>) that authenticate through
    /// the upstream SAML IdP, so the expected value can't drift between them.
    /// </summary>
    public const string ExpectedSamlRelayScriptHash = "sha256-IQKtK10TFgRroV/L1+sRadhw5yAEkHE3GlbgJgxr7K4=";

    /// <summary>
    /// Starts the upstream SAML IdP, the middle SAML SP/IdP, the downstream SAML SP,
    /// and (when <paramref name="includeOidcClient"/> is <c>true</c>) an additional
    /// downstream OIDC relying party registered against the middle with front-channel
    /// logout wired up (<see cref="Client.FrontChannelLogoutUri"/>,
    /// <see cref="Client.PostLogoutRedirectUris"/>, <see cref="GrantTypes.Code"/>,
    /// <c>RequireConsent = false</c>).
    /// </summary>
    /// <param name="configurator">The scenario/test host configurator.</param>
    /// <param name="includeOidcClient">Whether to also start a downstream OIDC relying party.</param>
    /// <param name="topologySuffix">
    /// A short, unique-per-scenario suffix (e.g. <c>"fed"</c>, <c>"oidc"</c>,
    /// <c>"upstream"</c>) appended to every host name/subdomain created by this
    /// topology. Each scenario that calls this helper must pass a distinct value so
    /// that host names, subdomains (and therefore cookies, which are not
    /// port-scoped), and SAML entity IDs cannot collide when multiple scenarios'
    /// fixtures are exercised concurrently.
    /// </param>
    /// <param name="ct">A cancellation token.</param>
    public static async Task<Result> StartAsync(
        IScenarioConfigurator configurator,
        bool includeOidcClient,
        string topologySuffix,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topologySuffix);

        var upstreamSigningCertificate = CreateSigningCertificate("CN=Federated Logout Upstream IdP");

        // Deferred: the middle host's entity id and ACS/SLO endpoints are only known
        // once the middle host has started (below), so this list is populated after
        // the middle host is up and the upstream IdP consults it lazily via the
        // in-memory store.
        var upstreamServiceProviders = new List<SamlServiceProvider>();

        var upstreamIdentityProvider = new IdentityServerTestHost(
            configurator,
            $"saml-upstream-idp-{topologySuffix}",
            identityServer => identityServer
                .AddSigningCredential(upstreamSigningCertificate)
                .AddSaml()
                .AddInMemorySamlServiceProviders(upstreamServiceProviders));
        upstreamIdentityProvider.AddDefaultUsers();
        upstreamIdentityProvider.SetIdentityResources(
        [
            new IdentityResources.OpenId(),
            new IdentityResources.Profile()
        ]);
        await upstreamIdentityProvider.StartAsync(ct);

        var middleSigningCertificate = CreateSigningCertificate("CN=Federated Logout Middle SP/IdP");

        // The middle's dynamic SAML SP module (representing the middle as an SP to
        // the upstream) needs its own SP signing certificate in order to sign the
        // outbound LogoutResponse it sends back to the upstream when the upstream
        // initiates SAML SLO. Without it, LogOutCommand.HandleRequest throws before
        // the combined federated-signout page can ever be rendered.
        var middleSpSigningCertificate = CreateSigningCertificate("CN=Federated Logout Middle SP Signing");
        var middleSpPublicCertificate = X509CertificateLoader.LoadCertificate(
            middleSpSigningCertificate.Export(X509ContentType.Cert));

        // Deferred: the downstream host's entity id and ACS/SLO endpoints are only
        // known once the downstream host has started (below), so this list is
        // populated after the downstream host is up.
        var downstreamServiceProviders = new List<SamlServiceProvider>();

        var upstreamProviders = new List<SamlProvider>
        {
            new()
            {
                Scheme = "saml-idp",
                DisplayName = "Upstream SAML IdP",
                Enabled = true,
                IdpEntityId = upstreamIdentityProvider.BuildUri("/Saml2").ToString(),
                SingleSignOnServiceUrl = upstreamIdentityProvider.BuildUri("/Saml2/SSO").ToString(),
                // Where the middle (acting as SP) sends its outbound LogoutResponse back
                // to the upstream after processing an upstream-initiated LogoutRequest.
                SingleLogoutServiceUrl = upstreamIdentityProvider.BuildUri("/Saml2/SLO").ToString(),
                SigningCertificateBase64 = Convert.ToBase64String(
                    upstreamSigningCertificate.Export(X509ContentType.Cert)),
                BindingType = "redirect",
                WantAssertionsSigned = false,
                SpSigningCertificateBase64 = Convert.ToBase64String(
                    middleSpSigningCertificate.Export(X509ContentType.Pfx)),
            }
        };

        // Downstream service provider (the middle acting as SAML IdP) is registered
        // below, once the downstream host has started and its dynamic port is known.
        var middleServiceProvider = new IdentityServerTestHost(
            configurator,
            $"saml-middle-sp-{topologySuffix}",
            identityServer => identityServer
                .AddSigningCredential(middleSigningCertificate)
                .AddSaml()
                .AddSamlDynamicProvider()
                .AddInMemorySamlProviders(upstreamProviders)
                .AddInMemorySamlServiceProviders(downstreamServiceProviders));

        middleServiceProvider.AddDefaultUsers();
        middleServiceProvider.SetIdentityResources(
        [
            new IdentityResources.OpenId(),
            new IdentityResources.Profile()
        ]);
        await middleServiceProvider.StartAsync(ct);

        upstreamServiceProviders.Add(new SamlServiceProvider
        {
            EntityId = middleServiceProvider.BuildUri().ToString().TrimEnd('/'),
            DisplayName = "Middle IdentityServer SAML SP",
            Enabled = true,
            AllowedScopes = new HashSet<string> { "openid", "profile" },
            AssertionConsumerServiceUrls =
            [
                new IndexedEndpoint
                {
                    Location = middleServiceProvider.BuildUri("/federation/saml-idp/Saml2/Acs").ToString(),
                    Binding = SamlBinding.HttpPost,
                    Index = 0,
                    IsDefault = true
                }
            ],
            SingleLogoutServiceUrls =
            [
                new SamlEndpointType
                {
                    Location = middleServiceProvider.BuildUri("/federation/saml-idp/Saml2/Logout").ToString(),
                    Binding = SamlBinding.HttpRedirect
                }
            ],
            SigningBehavior = SamlSigningBehavior.SignAssertion,
            RequireSignedAuthnRequests = false,
            // The middle always signs its outbound LogoutResponse (SigningServiceCertificate
            // is mandatory in the underlying SP module), so signature validation is not
            // actually bypassed even with RequireSignedLogoutResponses = false below; the
            // middle's SP signing certificate is still registered here so the upstream can
            // validate that signature.
            RequireSignedLogoutResponses = false,
            Certificates =
            [
                new ServiceProviderCertificate
                {
                    Certificate = middleSpPublicCertificate,
                    Use = KeyUse.Both
                }
            ]
        });

        var downstreamSigningCertificate = CreateSigningCertificate("CN=Federated Logout Downstream SP");

        var downstreamServiceProvider = new SamlClientTestHost(
            configurator,
            middleServiceProvider,
            downstreamSigningCertificate,
            $"saml-downstream-sp-{topologySuffix}");
        await downstreamServiceProvider.StartAsync(ct);

        var downstreamPublicCertificate = X509CertificateLoader.LoadCertificate(
            downstreamSigningCertificate.Export(X509ContentType.Cert));
        downstreamServiceProviders.Add(new SamlServiceProvider
        {
            EntityId = downstreamServiceProvider.EntityId,
            DisplayName = "Downstream SAML SP",
            Enabled = true,
            AllowedScopes = new HashSet<string> { "openid", "profile" },
            AssertionConsumerServiceUrls =
            [
                new IndexedEndpoint
                {
                    Location = downstreamServiceProvider.BuildUri("/Saml2/Acs").ToString(),
                    Binding = SamlBinding.HttpPost,
                    Index = 0,
                    IsDefault = true
                }
            ],
            SingleLogoutServiceUrls =
            [
                new SamlEndpointType
                {
                    Location = downstreamServiceProvider.BuildUri("/Saml2/Logout").ToString(),
                    Binding = SamlBinding.HttpRedirect
                }
            ],
            SigningBehavior = SamlSigningBehavior.SignAssertion,
            RequireSignedAuthnRequests = true,
            Certificates =
            [
                new ServiceProviderCertificate
                {
                    Certificate = downstreamPublicCertificate,
                    Use = KeyUse.Both
                }
            ]
        });

        ClientWebAppTestHost? downstreamOidcClient = null;
        if (includeOidcClient)
        {
            downstreamOidcClient = new ClientWebAppTestHost(
                configurator,
                middleServiceProvider,
                apiHost: null,
                name: $"oidc-downstream-webapp-{topologySuffix}");
            await downstreamOidcClient.StartAsync(ct);

            middleServiceProvider.AddClient(downstreamOidcClient, c =>
            {
                c.ClientId = downstreamOidcClient.Name;
                c.ClientName = downstreamOidcClient.Name;
                c.RequireConsent = false;
                c.AllowedGrantTypes = GrantTypes.Code;
                c.AllowedScopes =
                [
                    IdentityServerConstants.StandardScopes.OpenId,
                    IdentityServerConstants.StandardScopes.Profile
                ];
                c.PostLogoutRedirectUris = [downstreamOidcClient.BuildUri("signout-callback-oidc").ToString()];
                c.FrontChannelLogoutUri = downstreamOidcClient.BuildUri("signout-oidc").ToString();
            });
        }

        return new Result(
            upstreamIdentityProvider,
            middleServiceProvider,
            downstreamServiceProvider,
            downstreamOidcClient);
    }

    private static X509Certificate2 CreateSigningCertificate(string subjectName)
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            subjectName,
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, critical: true));

        var now = DateTimeOffset.UtcNow;
        using var certificate = request.CreateSelfSigned(now.AddDays(-1), now.AddYears(1));
        return X509CertificateLoader.LoadPkcs12(
            certificate.Export(X509ContentType.Pfx),
            null,
            X509KeyStorageFlags.Exportable);
    }

    /// <summary>
    /// The started hosts making up the mixed-protocol federated logout topology.
    /// <see cref="DownstreamOidcClient"/> is <c>null</c> unless requested via
    /// <c>includeOidcClient: true</c>.
    /// </summary>
    /// <remarks>
    /// Callers own the lifetime of every host below and must dispose them (reverse
    /// of creation order: OIDC client / downstream SAML SP first, then middle, then
    /// upstream last) from their own <c>StopAsync</c>.
    /// </remarks>
    public sealed record Result(
        IdentityServerTestHost UpstreamIdentityProvider,
        IdentityServerTestHost MiddleServiceProvider,
        SamlClientTestHost DownstreamServiceProvider,
        ClientWebAppTestHost? DownstreamOidcClient);
}
