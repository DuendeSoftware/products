// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.AspNetCore.Authentication.JwtBearer.DPoP;

/// <summary>
/// Controls how the issued at time of proof tokens is validated.
/// </summary>
public enum ExpirationMode
{
    /// <summary>
    /// Validate the time from the server-issued nonce.
    /// </summary>
    Nonce,
    /// <summary>
    /// Validate the time from the iat claim in the proof token.
    /// </summary>
    IssuedAt,
    /// <summary>
    /// Validate both the nonce and the iat claim.
    /// </summary>
    Both
}
