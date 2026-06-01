// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Authentication.Totp;

/// <summary>
/// Provides TOTP (Time-based One-Time Password) authentication for users.
/// </summary>
public interface ITotpAuthenticator
{
    /// <summary>
    /// Attempts to authenticate a user using a TOTP code from the specified device.
    /// </summary>
    /// <param name="subjectId">The subject ID of the user.</param>
    /// <param name="deviceName">The name of the TOTP device to verify against.</param>
    /// <param name="totp">The TOTP code submitted by the user.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<bool> TryAuthenticateAsync(UserSubjectId subjectId, TotpDeviceName deviceName, PlainTextTotp totp, Ct ct);
}
