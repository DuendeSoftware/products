// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.AspNetCore.Authentication.JwtBearer.DPoP;

/// <summary>
/// Result of nonce validation.
/// </summary>
public enum NonceValidationResult
{
    /// <summary>
    /// The nonce is valid.
    /// </summary>
    Valid,

    /// <summary>
    /// The nonce is missing from the proof token.
    /// </summary>
    Missing,

    /// <summary>
    /// The nonce is invalid (malformed, expired, etc.).
    /// </summary>
    Invalid
}
