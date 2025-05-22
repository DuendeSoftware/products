// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using Microsoft.IdentityModel.Tokens;

namespace Duende.IdentityServer.Configuration;

/// <summary>
/// Options for DPoP
/// </summary>
public class DPoPOptions
{
    /// <summary>
    /// Duration that DPoP proof tokens are considered valid. Defaults to 1 minute.
    /// </summary>
    public TimeSpan ProofTokenValidityDuration { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Clock skew used in validating DPoP proof token expiration using a server-generated nonce value. Defaults to zero.
    /// </summary>
    public TimeSpan ServerClockSkew { get; set; } = TimeSpan.FromMinutes(0);

    /// <summary>
    /// The allowed signing algorithms used in validating DPoP proof tokens. Defaults to:
    /// RSA256, RSA384, RSA512, PS256, PS384, PS512, ES256, ES384, ES512.
    /// </summary>
    ///
    /// <summary>
    /// <para>
    /// Specifies the allowed signature algorithms for DPoP proof tokens. The "alg" headers of proofs
    /// are validated against this collection, and the dpop_signing_alg_values_supported discovery property is populated
    /// with these values.
    /// </para>
    /// <para>
    /// Defaults to [RS256, RS384, RS512, PS256, PS384, PS512, ES256, ES384, ES512], which allows the RSA, Probabilistic
    /// RSA, or ECDSA signing algorithms with 256, 384, or 512-bit SHA hashing.
    /// </para>
    /// <para>
    /// If set to an empty collection, all algorithms (including symmetric algorithms) are allowed, and the
    /// dpop_signing_alg_values_supported will not be set. Explicitly listing the expected values is recommended.
    ///</para>
    /// </summary>
    public ICollection<string> SupportedDPoPSigningAlgorithms { get; set; } =
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
    ];
}
