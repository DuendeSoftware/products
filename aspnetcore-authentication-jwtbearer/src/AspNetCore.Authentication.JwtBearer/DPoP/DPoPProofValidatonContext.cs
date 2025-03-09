// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Claims;

namespace Duende.AspNetCore.Authentication.JwtBearer.DPoP;

/// <summary>
/// Provides contextual information about a DPoP proof during validation.
/// </summary>
public record DPoPProofValidationContext
{
    /// <summary>
    /// The ASP.NET Core authentication scheme triggering the validation
    /// </summary>
    public required string Scheme { get; init; }

    /// <summary>
    /// The HTTP URL to validate
    /// </summary>
    public required string Url { get; init; }

    /// <summary>
    /// The HTTP method to validate
    /// </summary>
    public required string Method { get; init; }

    /// <summary>
    /// The DPoP proof token to validate
    /// </summary>
    public required string ProofToken { get; init; }

    /// <summary>
    /// The access token
    /// </summary>
    public required string AccessToken { get; init; }

    /// <summary>
    /// The claims associated with the access token. 
    /// This is included separately from the <see cref="AccessToken"/> because getting the claims 
    /// might be an expensive operation (especially if the token is a reference token).
    /// </summary>
    public IEnumerable<Claim> AccessTokenClaims { get; init; } = [];
}