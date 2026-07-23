// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

namespace Duende.IdentityServer.Admin;

/// <summary>
/// Algorithms for hashing client secrets.
/// </summary>
public enum SecretHashAlgorithm
{
    /// <summary>SHA-256 (default).</summary>
    Sha256,

    /// <summary>SHA-512.</summary>
    Sha512
}
