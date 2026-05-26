// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using Duende.UserManagement.Authentication.Internal;

namespace Duende.UserManagement.Authentication.Passwords;

/// <summary>
/// Represents a password supplied in response to an authentication challenge.
/// Unlike <see cref="PlainTextPassword"/>, this type applies only minimal validation
/// (not null, not empty, and within the maximum length) so that authentication can
/// succeed regardless of the current password complexity policy.
/// </summary>
[StringValue]
public partial record NonValidatedPassword
{
    public static readonly int MaxLength =
        Pbkdf2MaxPasswordLength.For(new Pbkdf2Inputs().PseudorandomFunctionName);

    internal string Value { get; }

    public override string ToString() => GetType().ToString();

    /// <summary>
    /// Creates a <see cref="NonValidatedPassword"/> from the specified string.
    /// </summary>
    /// <param name="s">The plain text password string.</param>
    /// <returns>The constructed <see cref="NonValidatedPassword"/>.</returns>
    /// <exception cref="FormatException">Thrown when the password string fails validation.</exception>
    public static NonValidatedPassword Create(string s) =>
        TryCreate(s, out var result, out var errors)
            ? result
            : throw new FormatException($"The value is not a valid {nameof(NonValidatedPassword)}. {string.Join(" ", errors)}");

    /// <summary>
    /// Attempts to create a <see cref="NonValidatedPassword"/> from the specified string.
    /// </summary>
    public static bool TryCreate([NotNullWhen(true)] string? s, [NotNullWhen(true)] out NonValidatedPassword? result) =>
        TryCreate(s, out result, out _);

    /// <summary>
    /// Attempts to create a <see cref="NonValidatedPassword"/> from the specified string,
    /// returning validation error messages on failure.
    /// </summary>
    public static bool TryCreate(
        [NotNullWhen(true)] string? passwordString,
        [NotNullWhen(true)] out NonValidatedPassword? result,
        [NotNullWhen(false)] out IReadOnlyList<string>? errors)
    {
        result = null;
        errors = null;

        if (string.IsNullOrEmpty(passwordString))
        {
            errors = ["A password is required."];
            return false;
        }

        if (passwordString.Length > MaxLength)
        {
            errors = [$"Password must not exceed {MaxLength} characters."];
            return false;
        }

        result = new NonValidatedPassword(passwordString);
        return true;
    }
}
