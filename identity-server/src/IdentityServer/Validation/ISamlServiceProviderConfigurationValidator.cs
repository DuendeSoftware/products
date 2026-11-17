// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

namespace Duende.IdentityServer.Validation;

/// <summary>
/// Validator for SAML service provider configuration.
/// </summary>
public interface ISamlServiceProviderConfigurationValidator
{
    /// <summary>
    /// Determines whether the configuration of a SAML service provider is valid.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A task that completes when validation is finished. Errors are reported through the context.</returns>
    Task ValidateAsync(SamlServiceProviderConfigurationValidationContext context, Ct ct);
}
