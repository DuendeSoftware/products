// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System.Security.Cryptography.X509Certificates;
using Duende.IdentityServer.Internal.Saml.Sp;
using Duende.IdentityServer.Internal.Saml.Sp.AspNetCore;
using Duende.IdentityServer.Internal.Saml.Sp.Bindings;
using Duende.IdentityServer.Internal.Saml.Sp.Configuration;
using Duende.IdentityServer.Internal.Saml.Sp.Metadata;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SpIdentityProvider = Duende.IdentityServer.Internal.Saml.Sp.IdentityProvider;

namespace Duende.IdentityServer.Hosting.DynamicProviders;

internal sealed class SamlConfigureOptions : ConfigureAuthenticationOptions<Saml2Options, SamlProvider>
{
    // Stored separately because the base class field is private and we need
    // HttpContext.RequestServices in ResolvePublicOptions.
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IIssuerNameService _issuerNameService;
    private readonly TimeProvider _timeProvider;

    public SamlConfigureOptions(
        IHttpContextAccessor httpContextAccessor,
        IIssuerNameService issuerNameService,
        TimeProvider timeProvider,
        ILogger<SamlConfigureOptions> logger)
        : base(httpContextAccessor, logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _issuerNameService = issuerNameService ?? throw new ArgumentNullException(nameof(issuerNameService));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    protected override void Configure(ConfigureAuthenticationContext<Saml2Options, SamlProvider> context)
    {
        var provider = context.IdentityProvider;
        var options = context.AuthenticationOptions;

        // Resolve the public SamlAuthenticationOptions (triggers customer pipeline)
        var publicOptions = ResolvePublicOptions(context.IdentityProvider.Scheme);

        // SignInScheme / SignOutScheme: customer override > DynamicProviderOptions default
        options.SignInScheme = publicOptions?.SignInScheme ?? context.DynamicProviderOptions.SignInScheme;
        options.SignOutScheme = publicOptions?.SignOutScheme ?? context.DynamicProviderOptions.SignOutScheme;

        // Set the SP module path to the dynamic provider path prefix + /Saml2
        // so the handler intercepts paths like /federation/{scheme}/Saml2/Acs
        options.SPOptions.ModulePath = context.PathPrefix + "/Saml2";

        // Set SP entity ID: customer override > provider config > IdentityServer issuer
        var entityId = publicOptions?.SpEntityId
            ?? (!string.IsNullOrWhiteSpace(provider.SpEntityId) ? provider.SpEntityId : null)
            ?? _issuerNameService.GetCurrentAsync(default).GetAwaiter().GetResult();

        options.SPOptions.EntityId = new EntityId(entityId);

        // Configure the signing algorithm: customer override > provider config
        var outboundSigningAlgorithm = publicOptions?.OutboundSigningAlgorithm
            ?? provider.OutboundSigningAlgorithm;
        options.SPOptions.OutboundSigningAlgorithm = outboundSigningAlgorithm;

        // WantAssertionsSigned: customer override > provider config
        options.SPOptions.WantAssertionsSigned = publicOptions?.WantAssertionsSigned
            ?? provider.WantAssertionsSigned;

        // Add SP signing certificate if configured
        if (!string.IsNullOrWhiteSpace(provider.SpSigningCertificateBase64))
        {
            var certBytes = Convert.FromBase64String(provider.SpSigningCertificateBase64);
            var password = provider.SpSigningCertificatePassword;
            var cert = X509CertificateLoader.LoadPkcs12(certBytes, password);
            options.SPOptions.ServiceCertificates.Add(new ServiceCertificate
            {
                Certificate = cert,
                Use = CertificateUse.Signing
            });
        }

        // Build and register the remote IdP
        if (!string.IsNullOrWhiteSpace(provider.IdpEntityId))
        {
            var idp = BuildIdentityProvider(provider, options.SPOptions, _timeProvider, outboundSigningAlgorithm);
            options.IdentityProviders.Add(idp);
        }
    }

    private SamlAuthenticationOptions? ResolvePublicOptions(string? scheme)
    {
        if (string.IsNullOrWhiteSpace(scheme))
        {
            return null;
        }

        var optionsFactory = _httpContextAccessor.HttpContext?.RequestServices
            .GetService<IOptionsFactory<SamlAuthenticationOptions>>();

        return optionsFactory?.Create(scheme);
    }

    private static SpIdentityProvider BuildIdentityProvider(SamlProvider provider, SPOptions spOptions, TimeProvider timeProvider, string? outboundSigningAlgorithm)
    {
        var idp = new SpIdentityProvider(new EntityId(provider.IdpEntityId!), spOptions, timeProvider)
        {
            AllowUnsolicitedAuthnResponse = provider.AllowUnsolicitedAuthnResponse,
            WantAuthnRequestsSigned = false,
            DisableOutboundLogoutRequests = string.IsNullOrWhiteSpace(provider.SingleLogoutServiceUrl),
            OutboundSigningAlgorithm = outboundSigningAlgorithm,
        };

        // Set binding type
        idp.Binding = provider.BindingType.Equals("post", StringComparison.OrdinalIgnoreCase)
            ? Saml2BindingType.HttpPost
            : Saml2BindingType.HttpRedirect;

        // Set SSO URL
        if (!string.IsNullOrWhiteSpace(provider.SingleSignOnServiceUrl))
        {
            idp.SingleSignOnServiceUrl = new Uri(provider.SingleSignOnServiceUrl);
        }

        // Set SLO URL if provided
        if (!string.IsNullOrWhiteSpace(provider.SingleLogoutServiceUrl))
        {
            idp.SingleLogoutServiceUrl = new Uri(provider.SingleLogoutServiceUrl);
        }

        // Add signing certificate if provided
        if (!string.IsNullOrWhiteSpace(provider.SigningCertificateBase64))
        {
            var certBytes = Convert.FromBase64String(provider.SigningCertificateBase64);
            var cert = X509CertificateLoader.LoadCertificate(certBytes);
            idp.SigningKeys.AddConfiguredKey(cert);
        }

        return idp;
    }
}
