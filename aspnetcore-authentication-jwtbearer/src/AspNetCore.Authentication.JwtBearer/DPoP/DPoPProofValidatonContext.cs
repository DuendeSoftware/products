// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Claims;

namespace Duende.AspNetCore.Authentication.JwtBearer.DPoP;

/// <summary>
/// Provides contextual information about a DPoP proof during validation.
/// </summary>
public sealed record DPoPProofValidationContext
{
    /// <summary>
    /// The ASP.NET Core authentication scheme triggering the validation
    /// </summary>
    public required string Scheme { get; init; }

    /// <summary>
    /// The HTTP URL that is expected in the DPoP proof as the htu claim.
    /// </summary>
    public required string ExpectedUrl { get; init; }

    /// <summary>
    /// The HTTP method that is expected in the DPoP proof as the htm claim.
    /// </summary>
    public required string ExpectedMethod { get; init; }

    /// <summary>
    /// The DPoP proof token to validate
    /// </summary>
    public required string ProofToken { get; init; }

    /// <summary>
    /// The access token that is expected to be bound to the DPoP proof key
    /// </summary>
    public required string AccessToken { get; init; }

    /// <summary>
    /// The claims associated with the access token. 
    /// This is included separately from the <see cref="AccessToken"/> because getting the claims 
    /// might be an expensive operation (especially if the token is a reference token).
    /// </summary>
    public IEnumerable<Claim> AccessTokenClaims { get; init; } = [];

    /// <summary>
    /// The configured options to use when validating the DPoP proof.
    /// </summary>
    public required DPoPOptions Options { get; init; }
}
