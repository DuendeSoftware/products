// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

namespace Duende.IdentityServer.Validation;

/// <summary>
/// Validator for handling identity provider configuration
/// </summary>
public interface IIdentityProviderConfigurationValidator
{
    /// <summary>
    /// Determines whether the configuration of an identity provider is valid.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns></returns>
    Task ValidateAsync(IdentityProviderConfigurationValidationContext context, Ct ct);
}
