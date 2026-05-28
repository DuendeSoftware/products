// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using Microsoft.IdentityModel.Tokens;

namespace Duende.IdentityServer.Configuration;

/// <summary>
/// Settings for Demonstration of Proof-of-Possession (DPoP), which enables sender-constrained
/// access tokens that are cryptographically bound to a client's key pair.
/// </summary>
public class DPoPOptions
{
    /// <summary>
    /// Gets or sets how long a DPoP proof token is considered valid after it is issued.
    /// </summary>
    /// <remarks>
    /// Defaults to 1 minute. DPoP proof tokens are short-lived by design to prevent replay
    /// attacks. This window must be wide enough to account for clock differences between the
    /// client and server; see also <see cref="ServerClockSkew"/>.
    /// </remarks>
    public TimeSpan ProofTokenValidityDuration { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Gets or sets the clock skew tolerance applied when validating the expiration of DPoP proof tokens that
    /// use a server-generated nonce.
    /// </summary>
    /// <remarks>
    /// Defaults to zero. Increase this value if clients and the server have measurable clock
    /// drift and server-generated nonces are in use.
    /// </remarks>
    public TimeSpan ServerClockSkew { get; set; } = TimeSpan.FromMinutes(0);

    /// <summary>
    /// <para>
    /// Gets or sets the allowed signature algorithms for DPoP proof tokens. The "alg" headers of proofs
    /// are validated against this collection, and the dpop_signing_alg_values_supported discovery property is populated
    /// with these values.
    /// </para>
    /// <para>
    /// Defaults to [RS256, RS384, RS512, PS256, PS384, PS512, ES256, ES384, ES512], which allows the RSA, Probabilistic
    /// RSA, or ECDSA signing algorithms with 256, 384, or 512-bit SHA hashing.
    /// </para>
    /// <para>
    /// If set to an empty collection, no algorithms will be accepted and all DPoP proofs will be rejected.
    /// The dpop_signing_alg_values_supported discovery property will not be set. Explicitly listing the
    /// expected values is recommended.
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
