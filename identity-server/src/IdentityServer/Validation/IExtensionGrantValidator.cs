// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

namespace Duende.IdentityServer.Validation;

/// <summary>
/// Handles validation of token requests using custom grant types
/// </summary>
public interface IExtensionGrantValidator
{
    /// <summary>
    /// Validates the custom grant request.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>
    /// A principal
    /// </returns>
    Task ValidateAsync(ExtensionGrantValidationContext context, CT ct);

    /// <summary>
    /// Returns the grant type this validator can deal with
    /// </summary>
    /// <value>
    /// The type of the grant.
    /// </value>
    string GrantType { get; }
}
