// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.UserManagement.Authentication.Passkeys;
using Duende.UserManagement.Authentication.Passwords;
using Duende.UserManagement.Authentication.RecoveryCodes;
using Duende.UserManagement.Authentication.Totp;

namespace Duende.UserManagement.Authentication;

/// <summary>
/// Top-level options for configuring user authentication features.
/// </summary>
public sealed class UserAuthenticationOptions
{
    /// <summary>
    /// Configuration options for TOTP (Time-based One-Time Password) authentication.
    /// </summary>
    public TotpOptions Totp { get; } = new();

    /// <summary>
    /// Configuration options for passkey registration and authentication.
    /// </summary>
    public PasskeyOptions Passkeys { get; } = new();

    /// <summary>
    /// Configuration options for password hashing, validation, and history.
    /// </summary>
    public PasswordOptions Passwords { get; } = new();

    /// <summary>
    /// Configuration options for recovery code generation and authentication.
    /// </summary>
    public RecoveryCodeOptions RecoveryCodes { get; } = new();

    /// <summary>
    /// Configuration options for per-authenticator attempt throttling.
    /// </summary>
    public AuthenticationThrottlingOptions Throttling { get; } = new();
}
