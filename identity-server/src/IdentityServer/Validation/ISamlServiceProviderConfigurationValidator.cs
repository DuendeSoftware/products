// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

namespace Duende.IdentityServer.Validation;

/// <summary>
/// Validator for SAML service provider configuration.
/// </summary>
public interface ISamlServiceProviderConfigurationValidator
{
    Task ValidateAsync(SamlServiceProviderConfigurationValidationContext context);
}
