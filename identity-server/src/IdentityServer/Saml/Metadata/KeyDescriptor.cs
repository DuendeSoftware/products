// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using System.Security.Cryptography.Xml;
using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.Saml.Metadata;

/// <summary>
/// Metadata key descriptor
/// </summary>
public class KeyDescriptor
{
    /// <summary>
    /// Allowed use of the key. Default is Both as that's
    /// what an empty value means.
    /// </summary>
    public KeyUse Use { get; set; } = KeyUse.Both;

    /// <summary>
    /// Key info
    /// </summary>
    public KeyInfo? KeyInfo { get; set; }
}
