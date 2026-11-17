// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.UserManagement;
using Duende.UserManagement.Authentication.Passwords;

namespace PasswordValidation;

/// <summary>
/// A sample <see cref="IPasswordValidator"/> that rejects commonly used passwords.
/// In a real application, the blocklist would typically be loaded from a file or
/// database — for example, the NCSC "top 100k passwords" list or similar.
/// </summary>
public sealed class BlocklistPasswordValidator : IPasswordValidator
{
    private static readonly HashSet<string> Blocklist = new(StringComparer.OrdinalIgnoreCase)
    {
        // Small sample — a production blocklist would be much larger
        "password", "123456", "123456789", "qwerty", "abc123",
        "monkey", "1234567", "letmein", "trustno1", "dragon",
        "baseball", "iloveyou", "master", "sunshine", "ashley",
        "bailey", "passw0rd", "shadow", "123123", "654321",
    };

    /// <inheritdoc />
    public Task<PasswordValidationResult> ValidateAsync(UserSubjectId userId, string password, Ct ct) =>
        Task.FromResult<PasswordValidationResult>(
            Blocklist.Contains(password)
                ? new PasswordValidationResult.Rejected("Password appears in common password blocklist")
                : new PasswordValidationResult.Accepted());
}
