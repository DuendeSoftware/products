// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.IdentityServer.Validation;

/// <summary>
/// No-op SAML service provider configuration validator.
/// </summary>
public class NopSamlServiceProviderConfigurationValidator : ISamlServiceProviderConfigurationValidator
{
    public Task ValidateAsync(SamlServiceProviderConfigurationValidationContext context)
    {
        context.IsValid = true;
        return Task.CompletedTask;
    }
}
