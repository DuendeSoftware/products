// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Authentication.Totp;

/// <summary>
/// Provides TOTP (Time-based One-Time Password) authentication for users.
/// </summary>
public interface ITotpAuth
{
    /// <summary>
    /// Attempts to authenticate a user using a TOTP code from the specified authenticator.
    /// </summary>
    /// <param name="subjectId">The subject ID of the user.</param>
    /// <param name="authenticatorName">The name of the TOTP authenticator to verify against.</param>
    /// <param name="totp">The TOTP code submitted by the user.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<bool> TryAuthenticateAsync(UserSubjectId subjectId, TotpAuthenticatorName authenticatorName, PlainTextTotp totp, Ct ct);
}
