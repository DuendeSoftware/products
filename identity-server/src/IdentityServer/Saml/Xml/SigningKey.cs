// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using System.Security.Cryptography.X509Certificates;

namespace Duende.IdentityServer.Saml.Xml;

/// <summary>
/// Represents a signing key.
/// </summary>
public class SigningKey
{
    /// <summary>
    /// The asymmetric algorithm.
    /// </summary>
    public X509Certificate2? Certificate { get; set; }

    /// <summary>
    /// TrustLevel of the key. Defaults to ConfiguredKey because if you create
    /// a SigninKey yourself, the source is most likely configuration.
    /// </summary>
    public TrustLevel TrustLevel { get; init; } = TrustLevel.ConfiguredKey;
}
