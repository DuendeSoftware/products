// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityModel;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.IdentityModel.Tokens;

namespace Duende.AspNetCore.Authentication.JwtBearer.DPoP;

/// <summary>
/// Options for DPoP.
/// </summary>
public sealed class DPoPOptions
{
    /// <summary>
    /// Controls if bearer tokens are accepted in addition to DPoP tokens. If
    /// set, both Bearer and DPoP tokens can be used for authentication. If not,
    /// DPoP tokens must be used. Defaults to false.
    /// </summary>
    public bool AllowBearerTokens { get; set; }

    /// <summary>
    /// The amount of time that a proof token is valid for. Defaults to 5 seconds.
    /// </summary>
    public TimeSpan ProofTokenLifetime { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// The amount of time to add to account for clock skew when checking the
    /// issued at time supplied by the client in the form of the iat claim in
    /// the proof token. Defaults to 25 seconds.
    /// </summary>
    public TimeSpan ProofTokenIssuedAtClockSkew { get; set; } = TimeSpan.FromSeconds(25);

    /// <summary>
    /// The amount of time to add to account for clock skew when checking the
    /// issued at time supplied by the server (that is, by this API) in the form
    /// of a nonce. Defaults to 5 seconds.
    /// </summary>
    public TimeSpan ProofTokenNonceClockSkew { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Controls how the issued at time of proof tokens is validated. Defaults to <see
    /// cref="ExpirationMode.IssuedAt"/>.
    /// </summary>
    public ExpirationMode ProofTokenExpirationMode { get; set; } = ExpirationMode.IssuedAt;

    /// <summary>
    /// The maximum allowed length of a proof token, which is enforced to
    /// prevent resource-exhaustion attacks. Defaults to 4000 characters.
    /// </summary>
    public int ProofTokenMaxLength { get; set; } = 4000;

    /// <summary>
    /// The <see cref="TokenValidationParameters"/> used when validating DPoP proof tokens.
    /// </summary>
    /// <remarks>
    /// By default, the validation parameters are configured as follows:
    /// <list type="bullet">
    /// <item>Audience and Issuer validation are disabled, as they are not required in a DPoP proof.</item>
    /// <item>Lifetime validation is disabled, as complex lifetime checks are performed separately using the `iat` claim, a server-issued nonce, or both.</item>
    /// <item>Signatures are allowed from RSA, PSA, or ECDSA algorithms with key sizes of 256, 384, or 512 bits.</item>
    /// </list>
    /// </remarks>
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

    /// <summary>
    /// Prevent token replay attacks by caching used DPoP proof token jti values. To use this feature, you must register
    /// an implementation of <see cref="HybridCache"/> in the DI container.
    /// Defaults to true.
    /// </summary>
    public bool EnableReplayDetection { get; set; } = true;
}
