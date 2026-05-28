// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using Duende.IdentityServer.Validation;

namespace Duende.IdentityServer.Saml.Validation;

/// <summary>
/// Result of AuthnRequestValidation
/// </summary>
public class AuthnRequestValidationResult : ValidationResult
{
    /// <summary>
    /// Creates a valid validation result
    /// </summary>
    /// <param name="validatedAuthnRequest">Validated request</param>
    /// <returns>Valid validation result</returns>
    public static AuthnRequestValidationResult Valid(ValidatedAuthnRequest validatedAuthnRequest)
        => new()
        {
            ValidatedRequest = validatedAuthnRequest,
            IsError = false
        };

    /// <summary>
    /// Creates an invalid validation result.
    /// </summary>
    /// <param name="validatedAuthnRequest">The AuthnRequest validation context</param>
    /// <param name="saml2ErrorCode">Error code (a Saml2 status code)</param>
    /// <param name="errorDescription">Error description</param>
    /// <returns></returns>
    public static AuthnRequestValidationResult InValid(
        ValidatedAuthnRequest validatedAuthnRequest,
        string saml2ErrorCode,
        string? errorDescription = null)
        => new()
        {
            ValidatedRequest = validatedAuthnRequest,
            Error = saml2ErrorCode,
            ErrorDescription = errorDescription
        };

    /// <summary>
    /// The validated request.
    /// </summary>
    public required ValidatedAuthnRequest ValidatedRequest { get; init; }
}
