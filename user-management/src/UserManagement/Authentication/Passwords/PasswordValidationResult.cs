// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Authentication.Passwords;

/// <summary>
/// Represents the result of a password validation check.
/// </summary>
public abstract record PasswordValidationResult
{
    /// <summary>
    /// The password passed validation.
    /// </summary>
    public sealed record Accepted : PasswordValidationResult;

    /// <summary>
    /// The password failed validation.
    /// </summary>
    /// <param name="Reason">A human-readable explanation of why the password was rejected.</param>
    public sealed record Rejected(string Reason) : PasswordValidationResult;
}
