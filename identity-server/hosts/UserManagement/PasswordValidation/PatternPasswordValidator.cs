// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.UserManagement;
using Duende.UserManagement.Authentication.Passwords;

namespace PasswordValidation;

/// <summary>
/// A sample <see cref="IPasswordValidator"/> that rejects passwords containing
/// common keyboard patterns. Checks are intentionally simple and regex-free
/// to avoid ReDoS concerns.
/// </summary>
public sealed class PatternPasswordValidator : IPasswordValidator
{
    /// <summary>
    /// The minimum number of sequential characters (ascending or descending) that
    /// triggers rejection. Defaults to 4 (e.g., "abcd" or "4321").
    /// </summary>
    public int MaxSequentialChars { get; init; } = 4;

    /// <summary>
    /// The minimum number of identical repeated characters that triggers rejection.
    /// Defaults to 4 (e.g., "aaaa").
    /// </summary>
    public int MaxRepeatedChars { get; init; } = 4;

    private static readonly string[] KeyboardRows =
    [
        "qwertyuiop",
        "asdfghjkl",
        "zxcvbnm",
        "1234567890",
    ];

    /// <inheritdoc />
    public Task<PasswordValidationResult> ValidateAsync(UserSubjectId userId, string password, Ct ct)
    {
        ArgumentNullException.ThrowIfNull(password);

        if (HasSequentialChars(password, MaxSequentialChars))
        {
            return Reject("Password contains a sequence of sequential characters");
        }

        if (HasRepeatedChars(password, MaxRepeatedChars))
        {
            return Reject("Password contains too many repeated characters");
        }

        if (HasKeyboardWalk(password, MaxSequentialChars))
        {
            return Reject("Password contains a keyboard walk pattern");
        }

        return Task.FromResult<PasswordValidationResult>(new PasswordValidationResult.Accepted());
    }

    private static Task<PasswordValidationResult> Reject(string reason) =>
        Task.FromResult<PasswordValidationResult>(new PasswordValidationResult.Rejected(reason));

    /// <summary>
    /// Detects runs of consecutive Unicode code-points (ascending or descending),
    /// e.g. "abcd" or "4321".
    /// </summary>
    private static bool HasSequentialChars(string password, int threshold)
    {
        if (password.Length < threshold)
        {
            return false;
        }

        var run = 1;
        for (var i = 1; i < password.Length; i++)
        {
            var diff = password[i] - password[i - 1];
            if (diff is 1 or -1)
            {
                run++;
                if (run >= threshold)
                {
                    return true;
                }
            }
            else
            {
                run = 1;
            }
        }

        return false;
    }

    /// <summary>
    /// Detects runs of the same character, e.g. "aaaa".
    /// </summary>
    private static bool HasRepeatedChars(string password, int threshold)
    {
        if (password.Length < threshold)
        {
            return false;
        }

        var run = 1;
        for (var i = 1; i < password.Length; i++)
        {
            if (char.ToLowerInvariant(password[i]) == char.ToLowerInvariant(password[i - 1]))
            {
                run++;
                if (run >= threshold)
                {
                    return true;
                }
            }
            else
            {
                run = 1;
            }
        }

        return false;
    }

    /// <summary>
    /// Detects keyboard walk patterns (adjacent keys on common layouts).
    /// </summary>
    private static bool HasKeyboardWalk(string password, int threshold)
    {
        if (password.Length < threshold)
        {
            return false;
        }

        var run = 1;
        for (var i = 1; i < password.Length; i++)
        {
            if (AreAdjacentOnKeyboard(password[i - 1], password[i]))
            {
                run++;
                if (run >= threshold)
                {
                    return true;
                }
            }
            else
            {
                run = 1;
            }
        }

        return false;
    }

    private static bool AreAdjacentOnKeyboard(char a, char b)
    {
        var lowerA = char.ToLowerInvariant(a);
        var lowerB = char.ToLowerInvariant(b);

        foreach (var row in KeyboardRows)
        {
            var indexA = row.IndexOf(lowerA, StringComparison.Ordinal);
            if (indexA < 0)
            {
                continue;
            }

            var indexB = row.IndexOf(lowerB, StringComparison.Ordinal);
            if (indexB >= 0 && Math.Abs(indexA - indexB) == 1)
            {
                return true;
            }
        }

        return false;
    }
}
