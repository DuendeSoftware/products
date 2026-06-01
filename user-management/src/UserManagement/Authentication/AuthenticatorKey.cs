// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.UserManagement.Authentication.Totp;

namespace Duende.UserManagement.Authentication;

/// <summary>
/// Identifies the authenticator whose attempt state is being evaluated.
/// </summary>
public abstract record AuthenticatorKey
{
    private AuthenticatorKey() { }

    /// <summary>
    /// The user's password authenticator.
    /// </summary>
    public sealed record Password : AuthenticatorKey;
#pragma warning disable CA1724 // Type names should not match namespaces
    /// <summary>
    /// A named TOTP authenticator.
    /// </summary>
    /// <param name="Name">The name of the TOTP authenticator.</param>
    public sealed record Totp(TotpDeviceName Name) : AuthenticatorKey;
#pragma warning restore CA1724

    /// <summary>
    /// The user's recovery-code authenticator.
    /// </summary>
    public sealed record RecoveryCode : AuthenticatorKey;
}
