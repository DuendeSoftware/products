// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Authentication.Passwords;

/// <summary>
/// Validates a password beyond complexity rules. Implement this interface to add
/// custom password policy checks such as blocklist, breach database, or dictionary
/// validation. Multiple validators can be registered and are executed in order until one rejects.
/// </summary>
public interface IPasswordValidator
{
    /// <summary>
    /// Validates the specified password.
    /// </summary>
    /// <param name="userId">The subject ID of the user whose password is being validated.</param>
    /// <param name="password">The plain text password to validate.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A result indicating whether the password is acceptable.</returns>
    Task<PasswordValidationResult> ValidateAsync(UserSubjectId userId, string password, Ct ct);
}
