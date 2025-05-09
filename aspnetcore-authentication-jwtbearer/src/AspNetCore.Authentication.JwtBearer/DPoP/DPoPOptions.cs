// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityModel;
using Microsoft.IdentityModel.Tokens;

namespace Duende.AspNetCore.Authentication.JwtBearer.DPoP;

/// <summary>
/// Options for DPoP.
/// </summary>
public sealed class DPoPOptions
{
    /// <summary>
    /// Controls if both DPoP and Bearer tokens are allowed, or only DPoP. Defaults to <see cref="DPoPMode.DPoPOnly"/>.
    /// </summary>
    public DPoPMode TokenMode { get; set; } = DPoPMode.DPoPOnly;

    /// <summary>
    /// The amount of time that a proof token is valid for. Defaults to 1 second.
    /// </summary>
    public TimeSpan ProofTokenValidityDuration { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// The amount of time to add to account for clock skew when checking the
    /// issued at time supplied by the client in the form of the iat claim in
    /// the proof token. Defaults to 5 minutes.
    /// </summary>
    public TimeSpan ClientClockSkew { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// The amount of time to add to account for clock skew when checking the
    /// issued at time supplied by the server (that is, by this API) in the form
    /// of a nonce. Defaults to zero.
    /// </summary>
    public TimeSpan ServerClockSkew { get; set; } = TimeSpan.Zero;

    /// <summary>
    /// Controls how the issued at time of proof tokens is validated. Defaults to <see
    /// cref="ExpirationValidationMode.IssuedAt"/>.
    /// </summary>
    public ExpirationValidationMode ValidationMode { get; set; } = ExpirationValidationMode.IssuedAt;

    /// <summary>
    /// The maximum allowed length of a proof token, which is enforced to
    /// prevent resource-exhaustion attacks. Defaults to 4000 characters.
    /// </summary>
    public int ProofTokenMaxLength { get; set; } = 4000;

    public TokenValidationParameters ProofTokenValidationParameters = new()
    {
        ValidateAudience = false,
        ValidateIssuer = false,
        ValidateLifetime = false, // We validate lifetime manually, using either iat, server issued nonce, or both
        ValidTypes = [JwtClaimTypes.JwtTypes.DPoPProofToken],
        ValidAlgorithms =
        [
            SecurityAlgorithms.RsaSha256,
            SecurityAlgorithms.RsaSha384,
            SecurityAlgorithms.RsaSha512,

            SecurityAlgorithms.RsaSsaPssSha256,
            SecurityAlgorithms.RsaSsaPssSha384,
            SecurityAlgorithms.RsaSsaPssSha512,

            SecurityAlgorithms.EcdsaSha256,
            SecurityAlgorithms.EcdsaSha384,
            SecurityAlgorithms.EcdsaSha512
        ],
    };
}
