// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Authentication.Totp;

/// <summary>
/// Configuration options for TOTP authentication.
/// </summary>
public sealed class TotpOptions
{
    /// <summary>
    /// Gets the storage options for TOTP authenticator data.
    /// </summary>
    public StorageOptions Storage { get; } = new();
}
