// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using System.Threading.Tasks;

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
    /// <returns></returns>
    Task ValidateAsync(IdentityProviderConfigurationValidationContext context);
}
