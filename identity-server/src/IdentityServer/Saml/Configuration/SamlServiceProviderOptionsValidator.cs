// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Microsoft.Extensions.Options;

namespace Duende.IdentityServer.Saml.Configuration;

/// <summary>
/// Validates <see cref="SamlServiceProviderOptions"/> at startup to ensure
/// required properties are set.
/// </summary>
internal sealed class SamlServiceProviderOptionsValidator : IValidateOptions<SamlServiceProviderOptions>
{
    private readonly string _scheme;

    public SamlServiceProviderOptionsValidator(string scheme) => _scheme = scheme;

    public ValidateOptionsResult Validate(string? name, SamlServiceProviderOptions options)
    {
        if (!string.Equals(name, _scheme, StringComparison.Ordinal))
        {
            return ValidateOptionsResult.Skip;
        }

        if (string.IsNullOrWhiteSpace(options.SpEntityId))
        {
            return ValidateOptionsResult.Fail(
                $"SamlServiceProviderOptions.SpEntityId is required for scheme '{_scheme}'.");
        }

        if (string.IsNullOrWhiteSpace(options.IdpEntityId))
        {
            return ValidateOptionsResult.Fail(
                $"SamlServiceProviderOptions.IdpEntityId is required for scheme '{_scheme}'.");
        }

        if (string.IsNullOrWhiteSpace(options.SingleSignOnServiceUrl))
        {
            return ValidateOptionsResult.Fail(
                $"SamlServiceProviderOptions.SingleSignOnServiceUrl is required for scheme '{_scheme}'.");
        }

        if (!Uri.TryCreate(options.SingleSignOnServiceUrl, UriKind.Absolute, out _))
        {
            return ValidateOptionsResult.Fail(
                $"SamlServiceProviderOptions.SingleSignOnServiceUrl must be a valid absolute URL for scheme '{_scheme}'.");
        }

        if (!string.IsNullOrWhiteSpace(options.SingleLogoutServiceUrl) &&
            !Uri.TryCreate(options.SingleLogoutServiceUrl, UriKind.Absolute, out _))
        {
            return ValidateOptionsResult.Fail(
                $"SamlServiceProviderOptions.SingleLogoutServiceUrl must be a valid absolute URL for scheme '{_scheme}'.");
        }

        for (var i = 0; i < options.SigningCertificatesBase64.Count; i++)
        {
            var certBase64 = options.SigningCertificatesBase64[i];
            if (string.IsNullOrWhiteSpace(certBase64))
            {
                continue;
            }

            try
            {
                var certBytes = Convert.FromBase64String(certBase64);
                using var cert = System.Security.Cryptography.X509Certificates.X509CertificateLoader.LoadCertificate(certBytes);
            }
            catch (Exception ex) when (ex is FormatException or System.Security.Cryptography.CryptographicException)
            {
                return ValidateOptionsResult.Fail(
                    $"SamlServiceProviderOptions.SigningCertificatesBase64[{i}] is not a valid base64-encoded X.509 certificate for scheme '{_scheme}'.");
            }
        }

        if (!Enum.IsDefined(options.BindingType))
        {
            return ValidateOptionsResult.Fail(
                $"SamlServiceProviderOptions.BindingType has an invalid value '{options.BindingType}' for scheme '{_scheme}'.");
        }

        return ValidateOptionsResult.Success;
    }
}
