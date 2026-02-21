// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

namespace Duende.IdentityServer.Validation;

/// <summary>
/// Validator for handling client authentication
/// </summary>
public interface IClientConfigurationValidator
{
    /// <summary>
    /// Determines whether the configuration of a client is valid.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns></returns>
    Task ValidateAsync(ClientConfigurationValidationContext context, CT ct);
}
