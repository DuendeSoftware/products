// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Security.Cryptography.X509Certificates;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.Validation;

/// <summary>
/// Default identity provider configuration validator
/// </summary>
/// <seealso cref="IIdentityProviderConfigurationValidator" />
public class DefaultIdentityProviderConfigurationValidator : IIdentityProviderConfigurationValidator
{
    private readonly IdentityServerOptions _options;

    /// <summary>
    /// Constructor for DefaultIdentityProviderConfigurationValidator
    /// </summary>
    public DefaultIdentityProviderConfigurationValidator(IdentityServerOptions options) => _options = options;

    /// <inheritdoc/>
    public virtual async Task ValidateAsync(IdentityProviderConfigurationValidationContext context, Ct ct)
    {
        using var activity = Tracing.ValidationActivitySource.StartActivity("DefaultIdentityProviderConfigurationValidator.Validate");

        var type = _options.DynamicProviders.FindProviderType(context.IdentityProvider.Type);
        if (type == null)
        {
            context.SetError("IdentityProvider Type has not been registered with AddProviderType on the DynamicProviderOptions.");
            return;
        }

        if (string.IsNullOrWhiteSpace(context.IdentityProvider.Scheme))
        {
            context.SetError("Scheme is missing.");
            return;
        }

        if (context.IdentityProvider is OidcProvider oidc)
        {
            var oidcContext = new IdentityProviderConfigurationValidationContext<OidcProvider>(oidc);
            await ValidateOidcProviderAsync(oidcContext);

            if (!oidcContext.IsValid)
            {
                context.SetError(oidcContext.ErrorMessage);
            }

            return;
        }

        if (context.IdentityProvider is SamlProvider saml)
        {
            var samlContext = new IdentityProviderConfigurationValidationContext<SamlProvider>(saml);
            await ValidateSamlProviderAsync(samlContext);

            if (!samlContext.IsValid)
            {
                context.SetError(samlContext.ErrorMessage);
            }

            return;
        }
    }

    /// <summary>
    /// Validates the OIDC identity provider.
    /// </summary>
    /// <returns>A string that represents the error. Null if there is no error.</returns>
    protected virtual Task ValidateOidcProviderAsync(IdentityProviderConfigurationValidationContext<OidcProvider> context)
    {
        if (string.IsNullOrWhiteSpace(context.IdentityProvider.Authority))
        {
            context.SetError("Authority is missing.");
        }

        if (string.IsNullOrWhiteSpace(context.IdentityProvider.ClientId))
        {
            context.SetError("ClientId is missing.");
        }

        if (string.IsNullOrWhiteSpace(context.IdentityProvider.ResponseType))
        {
            context.SetError("ResponseType is missing.");
        }

        if (string.IsNullOrWhiteSpace(context.IdentityProvider.Scope))
        {
            context.SetError("Scope is missing.");
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Validates the SAML identity provider.
    /// </summary>
    protected virtual Task ValidateSamlProviderAsync(IdentityProviderConfigurationValidationContext<SamlProvider> context)
    {
        if (string.IsNullOrWhiteSpace(context.IdentityProvider.IdpEntityId))
        {
            context.SetError("IdpEntityId is missing.");
            return Task.CompletedTask;
        }

        if (string.IsNullOrWhiteSpace(context.IdentityProvider.SingleSignOnServiceUrl))
        {
            context.SetError("SingleSignOnServiceUrl is missing.");
            return Task.CompletedTask;
        }

        if (!Uri.TryCreate(context.IdentityProvider.SingleSignOnServiceUrl, UriKind.Absolute, out _))
        {
            context.SetError("SingleSignOnServiceUrl is not a valid absolute URL.");
            return Task.CompletedTask;
        }

        if (!string.IsNullOrWhiteSpace(context.IdentityProvider.SingleLogoutServiceUrl) &&
            !Uri.TryCreate(context.IdentityProvider.SingleLogoutServiceUrl, UriKind.Absolute, out _))
        {
            context.SetError("SingleLogoutServiceUrl is not a valid absolute URL.");
            return Task.CompletedTask;
        }

        if (!string.IsNullOrWhiteSpace(context.IdentityProvider.SigningCertificateBase64))
        {
            try
            {
                var certBytes = Convert.FromBase64String(context.IdentityProvider.SigningCertificateBase64);
                X509CertificateLoader.LoadCertificate(certBytes);
            }
            catch (Exception)
            {
                context.SetError("SigningCertificateBase64 is not a valid X.509 certificate.");
                return Task.CompletedTask;
            }
        }

        if (!string.IsNullOrWhiteSpace(context.IdentityProvider.SpSigningCertificateBase64))
        {
            try
            {
                var certBytes = Convert.FromBase64String(context.IdentityProvider.SpSigningCertificateBase64);
                var password = context.IdentityProvider.SpSigningCertificatePassword;
                X509CertificateLoader.LoadPkcs12(certBytes, password);
            }
            catch (Exception)
            {
                context.SetError("SpSigningCertificateBase64 is not a valid PKCS#12 certificate.");
                return Task.CompletedTask;
            }
        }

        var binding = context.IdentityProvider.BindingType;
        if (!binding.Equals("redirect", StringComparison.OrdinalIgnoreCase) &&
            !binding.Equals("post", StringComparison.OrdinalIgnoreCase))
        {
            context.SetError("BindingType must be 'redirect' or 'post'.");
        }

        return Task.CompletedTask;
    }
}

