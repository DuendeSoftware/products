// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.AspNetCore.Authentication.JwtBearer.DPoP;

/// <summary>
/// Service for creating and validating DPoP server-issued nonces.
/// Implementers should focus on the nonce format and validity logic.
/// Error handling and nonce generation on failure are handled by the framework.
/// </summary>
public interface IDPoPNonceValidator
{
    /// <summary>
    /// Creates a new nonce value to return to the client.
    /// </summary>
    /// <param name="context">The DPoP proof validation context.</param>
    /// <returns>A nonce string to be returned to the client.</returns>
    string CreateNonce(DPoPProofValidationContext context);

    /// <summary>
    /// Validates the nonce value from the DPoP proof token.
    /// </summary>
    /// <param name="context">The DPoP proof validation context.</param>
    /// <param name="nonce">The nonce value to validate (may be null or empty).</param>
    /// <returns>The validation result indicating whether the nonce is valid, missing, or invalid.</returns>
    NonceValidationResult ValidateNonce(DPoPProofValidationContext context, string? nonce);
}
