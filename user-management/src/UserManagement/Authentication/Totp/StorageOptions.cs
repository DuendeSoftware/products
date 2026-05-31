// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Authentication.Totp;

/// <summary>
/// Configuration options for TOTP authenticator key storage.
/// </summary>
public sealed class StorageOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether TOTP keys should be protected at rest using data protection.
    /// Defaults to <c>true</c>.
    /// </summary>
    public bool ProtectKeys { get; set; } = true;
}
