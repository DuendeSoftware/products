// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using System.Security.Claims;
using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.Validation;

/// <summary>
/// Models the context for validaing DPoP proof tokens.
/// </summary>
public class DPoPProofValidatonContext
{
    /// <summary>
    /// Enum setting to control validation for the DPoP proof token expiration.
    /// This supports both the client generated 'iat' value and/or the server generated 'nonce' value. 
    /// Defaults to only using the 'iat' value.
    /// </summary>
    public DPoPTokenExpirationValidationMode ExpirationValidationMode { get; set; } = DPoPTokenExpirationValidationMode.Iat;

    /// <summary>
    /// Clock skew used in validating the DPoP proof token 'iat' claim value. Defaults to 5 minutes.
    /// </summary>
    public TimeSpan ClientClockSkew { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// The HTTP URL to validate
    /// </summary>
    public string Url { get; set; } = default!;

    /// <summary>
    /// The HTTP method to validate
    /// </summary>
    public string Method { get; set; } = default!;

    /// <summary>
    /// The DPoP proof token to validate
    /// </summary>
    public string ProofToken { get; set; } = default!;

    /// <summary>
    /// If the access token should also be validated
    /// </summary>
    public bool ValidateAccessToken { get; set; }

    /// <summary>
    /// The access token to validate if <see cref="ValidateAccessToken"/> is set
    /// </summary>
    public string? AccessToken { get; set; }

    /// <summary>
    /// The claims associated with the access token, used if <see cref="ValidateAccessToken"/> is set. 
    /// This is included separately from the <see cref="AccessToken"/> because getting the claims 
    /// might be an expensive operation (especially if the token is a reference token).
    /// </summary>
    public IEnumerable<Claim> AccessTokenClaims { get; set; } = [];
}
