// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

namespace Duende.IdentityServer.Validation;

/// <summary>
/// Allows inserting custom token validation logic
/// </summary>
public interface ICustomTokenValidator
{
    /// <summary>
    /// Custom validation logic for access tokens.
    /// </summary>
    /// <param name="result">The validation result so far.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The validation result</returns>
    Task<TokenValidationResult> ValidateAccessTokenAsync(TokenValidationResult result, CT ct);

    /// <summary>
    /// Custom validation logic for identity tokens.
    /// </summary>
    /// <param name="result">The validation result so far.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The validation result</returns>
    Task<TokenValidationResult> ValidateIdentityTokenAsync(TokenValidationResult result, CT ct);
}
