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
    /// Clock skew used in validating DPoP proof token expiration using a server-generated nonce value. Defaults to ten seconds.
    /// </summary>
    public TimeSpan ServerClockSkew { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// The allowed signing algorithms used in validating DPoP proof tokens. Defaults to:
    /// RSA256, RSA384, RSA512, PS256, PS384, PS512, ES256, ES384, ES512.
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
