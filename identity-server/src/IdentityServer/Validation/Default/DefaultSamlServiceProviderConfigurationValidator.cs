// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.IdentityServer.Validation;

/// <summary>
/// Default SAML service provider configuration validator.
/// </summary>
public class DefaultSamlServiceProviderConfigurationValidator : ISamlServiceProviderConfigurationValidator
{
    public async Task ValidateAsync(SamlServiceProviderConfigurationValidationContext context)
    {
        using var activity = Tracing.ValidationActivitySource.StartActivity(
            "DefaultSamlServiceProviderConfigurationValidator.Validate");

        await ValidateEntityIdAsync(context);
        if (!context.IsValid)
        {
            return;
        }

        await ValidateUriSchemesAsync(context);
        if (!context.IsValid)
        {
            return;
        }

        await ValidateEncryptionAsync(context);
    }

    protected virtual Task ValidateEntityIdAsync(SamlServiceProviderConfigurationValidationContext context)
    {
        if (string.IsNullOrWhiteSpace(context.ServiceProvider.EntityId))
        {
            context.SetError("EntityId is required.");
        }
        return Task.CompletedTask;
    }

    protected virtual Task ValidateUriSchemesAsync(SamlServiceProviderConfigurationValidationContext context)
    {
        var sp = context.ServiceProvider;

        if (sp.AssertionConsumerServiceUrls != null)
        {
            foreach (var url in sp.AssertionConsumerServiceUrls)
            {
                if (!url.IsAbsoluteUri)
                {
                    context.SetError($"Assertion Consumer Service URL '{url}' is not an absolute URI.");
                    return Task.CompletedTask;
                }

                if (!string.Equals(url.Scheme, "https", StringComparison.OrdinalIgnoreCase))
                {
                    context.SetError($"Assertion Consumer Service URL '{url}' does not use HTTPS scheme.");
                    return Task.CompletedTask;
                }
            }
        }

        if (sp.SingleLogoutServiceUrl?.Location != null)
        {
            if (!sp.SingleLogoutServiceUrl.Location.IsAbsoluteUri)
            {
                context.SetError($"Single Logout Service URL '{sp.SingleLogoutServiceUrl.Location}' is not an absolute URI.");
                return Task.CompletedTask;
            }

            if (!string.Equals(sp.SingleLogoutServiceUrl.Location.Scheme, "https", StringComparison.OrdinalIgnoreCase))
            {
                context.SetError($"Single Logout Service URL '{sp.SingleLogoutServiceUrl.Location}' does not use HTTPS scheme.");
                return Task.CompletedTask;
            }
        }
        return Task.CompletedTask;
    }

    protected virtual Task ValidateEncryptionAsync(SamlServiceProviderConfigurationValidationContext context)
    {
        if (context.ServiceProvider.EncryptAssertions &&
            (context.ServiceProvider.EncryptionCertificates == null ||
             context.ServiceProvider.EncryptionCertificates.Count == 0))
        {
            context.SetError("Encryption certificates are required when EncryptAssertions is true.");
        }
        return Task.CompletedTask;
    }
}
