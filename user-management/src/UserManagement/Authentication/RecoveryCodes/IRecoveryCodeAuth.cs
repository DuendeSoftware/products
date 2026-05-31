// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Authentication.RecoveryCodes;

/// <summary>
/// Provides recovery code authentication for users who cannot access their primary authenticators.
/// </summary>
public interface IRecoveryCodeAuth
{
    /// <summary>
    /// Attempts to authenticate a user using a recovery code. Consumes the code on success.
    /// </summary>
    /// <param name="subjectId">The subject ID of the user.</param>
    /// <param name="recoveryCode">The plain text recovery code submitted by the user.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<bool> TryAuthenticateAsync(UserSubjectId subjectId, PlainTextRecoveryCode recoveryCode, Ct ct);
}
