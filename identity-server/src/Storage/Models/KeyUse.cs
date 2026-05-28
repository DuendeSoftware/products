// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.IdentityServer.Models;

/// <summary>
/// Usages allowed for a key
/// </summary>
[Flags]
public enum KeyUse
{
    /// <summary>
    /// Valid for signing
    /// </summary>
    Signing = 1,

    /// <summary>
    /// Valid for encryption
    /// </summary>
    Encryption = 2,

    /// <summary>
    /// Both encryption and signing.
    /// </summary>
    Both = 3,
}
