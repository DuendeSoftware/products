// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityModel;

namespace Duende.AspNetCore.Authentication.JwtBearer.DPoP;

/// <summary>
/// Describes the result of validating a DPoP Proof.
/// </summary>
public sealed class DPoPProofValidationResult
{
    /// <summary>
    /// Indicates if the result was successful or not
    /// </summary>
    public bool IsError { get; private set; }

    /// <summary>
    /// The error code for the validation result
    /// </summary>
    public string? Error { get; private set; }

    /// <summary>
    /// The error description code for the validation result
    /// </summary>
    public string? ErrorDescription { get; private set; }

    /// <summary>
    /// The serialized JWK from the validated DPoP proof token.
    /// </summary>
    public string? JsonWebKey { get; set; }

    /// <summary>
    /// The JWK thumbprint from the validated DPoP proof token.
    /// </summary>
    public string? JsonWebKeyThumbprint { get; set; }

    /// <summary>
    /// The cnf value for the DPoP proof token 
    /// </summary>
    public string? Confirmation { get; set; }

    /// <summary>
    /// The payload value of the DPoP proof token.
    /// </summary>
    public IDictionary<string, object>? Payload { get; internal set; }

    /// <summary>
    /// The SHA256 hash of the jti value read from the payload.
    /// </summary>
    public string? TokenIdHash { get; set; }

    /// <summary>
    /// The ath value read from the payload.
    /// </summary>
    public string? AccessTokenHash { get; set; }

    /// <summary>
    /// The nonce value read from the payload.
    /// </summary>
    public string? Nonce { get; set; }

    /// <summary>
    /// The iat value read from the payload.
    /// </summary>
    public long? IssuedAt { get; set; }

    /// <summary>
    /// The nonce value issued by the server.
    /// </summary>
    public string? ServerIssuedNonce { get; set; }

    /// <summary>
    /// Sets the error properties of the result.
    /// </summary>
    public void SetError(string description, string error = OidcConstants.TokenErrors.InvalidDPoPProof)
    {
        Error = error;
        ErrorDescription = description;
        IsError = true;
    }
}
