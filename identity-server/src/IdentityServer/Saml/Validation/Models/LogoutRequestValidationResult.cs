// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using Duende.IdentityServer.Validation;

namespace Duende.IdentityServer.Saml.Validation;

/// <summary>
/// Result of LogoutRequest validation
/// </summary>
public class LogoutRequestValidationResult : ValidationResult
{
    /// <summary>
    /// Creates a valid validation result
    /// </summary>
    /// <param name="validatedLogoutRequest">Validated request</param>
    /// <returns>Valid validation result</returns>
    public static LogoutRequestValidationResult Valid(ValidatedLogoutRequest validatedLogoutRequest)
        => new()
        {
            ValidatedRequest = validatedLogoutRequest,
            IsError = false
        };

    /// <summary>
    /// Creates an invalid validation result
    /// </summary>
    /// <param name="validatedLogoutRequest">The LogoutRequest validation context</param>
    /// <param name="saml2ErrorCode">Error code (a Saml2 status code)</param>
    /// <param name="errorDescription">Error description</param>
    /// <returns>Invalid validation result</returns>
    public static LogoutRequestValidationResult InValid(
        ValidatedLogoutRequest validatedLogoutRequest,
        string saml2ErrorCode,
        string? errorDescription = null)
        => new()
        {
            ValidatedRequest = validatedLogoutRequest,
            Error = saml2ErrorCode,
            ErrorDescription = errorDescription
        };

    /// <summary>
    /// The validated request
    /// </summary>
    public required ValidatedLogoutRequest ValidatedRequest { get; init; }

    /// <summary>
    /// Indicates whether a matching SAML session was found for the requesting SP.
    /// When <see langword="false"/>, the IdP should respond with Success without
    /// terminating the user's IdP session (per SAML 2.0 Profiles §4.4).
    /// </summary>
    public bool SessionFound { get; init; } = true;
}
