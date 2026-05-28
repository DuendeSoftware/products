// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System.Security.Cryptography.X509Certificates;
using Duende.IdentityServer.Internal.Saml.Sp.Bindings;
using Duende.IdentityServer.Internal.Saml.Sp.Configuration;
using Duende.IdentityServer.Internal.Saml.Sp.Metadata;
using Duende.IdentityServer.Saml.Configuration;
using Microsoft.Extensions.Options;
using Saml2HandlerOptions = Duende.IdentityServer.Internal.Saml.Sp.AspNetCore.Saml2Options;
using SpIdentityProvider = Duende.IdentityServer.Internal.Saml.Sp.IdentityProvider;

namespace Duende.IdentityServer.Internal.Saml.Sp;

/// <summary>
/// Configures the internal <see cref="Saml2HandlerOptions"/> from the public
/// <see cref="SamlServiceProviderOptions"/> for standalone (non-dynamic-provider) usage.
/// Only applies to the scheme name specified at construction time.
/// </summary>
internal sealed class ConfigureSaml2OptionsFromServiceProvider : IConfigureNamedOptions<Saml2HandlerOptions>
{
    private readonly string _scheme;
    private readonly IOptionsMonitor<SamlServiceProviderOptions> _spOptionsMonitor;
    private readonly TimeProvider _timeProvider;

    public ConfigureSaml2OptionsFromServiceProvider(
        string scheme,
        IOptionsMonitor<SamlServiceProviderOptions> spOptionsMonitor,
        TimeProvider timeProvider)
    {
        _scheme = scheme;
        _spOptionsMonitor = spOptionsMonitor;
        _timeProvider = timeProvider;
    }

    public void Configure(Saml2HandlerOptions options) { }

    public void Configure(string? name, Saml2HandlerOptions options)
    {
        if (!string.Equals(name, _scheme, StringComparison.Ordinal))
        {
            return;
        }

        var spOptions = _spOptionsMonitor.Get(_scheme);

        options.SPOptions.ModulePath = string.IsNullOrWhiteSpace(spOptions.ModulePath)
            ? SamlServiceProviderDefaults.ModulePath
            : spOptions.ModulePath;

        if (!string.IsNullOrWhiteSpace(spOptions.OutboundSigningAlgorithm))
        {
            options.SPOptions.OutboundSigningAlgorithm = spOptions.OutboundSigningAlgorithm;
        }

        options.SPOptions.WantAssertionsSigned = spOptions.WantAssertionsSigned;

        // Add SP signing certificate if configured
        if (!string.IsNullOrWhiteSpace(spOptions.SpSigningCertificateBase64))
        {
            var certBytes = Convert.FromBase64String(spOptions.SpSigningCertificateBase64);
            var password = spOptions.SpSigningCertificatePassword;
            var cert = X509CertificateLoader.LoadPkcs12(certBytes, password);
            options.SPOptions.ServiceCertificates.Add(new ServiceCertificate
            {
                Certificate = cert,
                Use = CertificateUse.Signing
            });
        }

        if (!string.IsNullOrWhiteSpace(spOptions.SpEntityId))
        {
            options.SPOptions.EntityId = new EntityId(spOptions.SpEntityId);
        }

        if (!string.IsNullOrWhiteSpace(spOptions.SignInScheme))
        {
            options.SignInScheme = spOptions.SignInScheme;
        }

        if (!string.IsNullOrWhiteSpace(spOptions.SignOutScheme))
        {
            options.SignOutScheme = spOptions.SignOutScheme;
        }

        if (!string.IsNullOrWhiteSpace(spOptions.IdpEntityId))
        {
            var idp = BuildIdentityProvider(spOptions, options.SPOptions, _timeProvider);
            options.IdentityProviders.Add(idp);
        }
    }

    private static SpIdentityProvider BuildIdentityProvider(
        SamlServiceProviderOptions provider, SPOptions spOptions, TimeProvider timeProvider)
    {
        var idp = new SpIdentityProvider(new EntityId(provider.IdpEntityId!), spOptions, timeProvider)
        {
            AllowUnsolicitedAuthnResponse = provider.AllowUnsolicitedAuthnResponse,
            WantAuthnRequestsSigned = false,
            DisableOutboundLogoutRequests = string.IsNullOrWhiteSpace(provider.SingleLogoutServiceUrl),
        };

        idp.Binding = MapBindingType(provider.BindingType);

        if (!string.IsNullOrWhiteSpace(provider.OutboundSigningAlgorithm))
        {
            idp.OutboundSigningAlgorithm = provider.OutboundSigningAlgorithm;
        }

        if (!string.IsNullOrWhiteSpace(provider.SingleSignOnServiceUrl))
        {
            idp.SingleSignOnServiceUrl = new Uri(provider.SingleSignOnServiceUrl);
        }

        if (!string.IsNullOrWhiteSpace(provider.SingleLogoutServiceUrl))
        {
            idp.SingleLogoutServiceUrl = new Uri(provider.SingleLogoutServiceUrl);
        }

        foreach (var certBase64 in provider.SigningCertificatesBase64)
        {
            if (!string.IsNullOrWhiteSpace(certBase64))
            {
                var certBytes = Convert.FromBase64String(certBase64);
                var cert = X509CertificateLoader.LoadCertificate(certBytes);
                idp.SigningKeys.AddConfiguredKey(cert);
            }
        }

        return idp;
    }

    private static Saml2BindingType MapBindingType(SamlBindingType bindingType) =>
        bindingType switch
        {
            SamlBindingType.HttpRedirect => Saml2BindingType.HttpRedirect,
            SamlBindingType.HttpPost => Saml2BindingType.HttpPost,
            _ => throw new InvalidOperationException(
                $"Unsupported SAML binding type '{bindingType}'.")
        };
}
