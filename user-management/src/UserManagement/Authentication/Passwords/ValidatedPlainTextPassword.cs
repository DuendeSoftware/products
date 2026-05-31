// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.UserManagement.Authentication.Passwords.Internal;

namespace Duende.UserManagement.Authentication.Passwords;

/// <summary>
/// Represents a plain text password that has been validated against the configured password policy.
/// </summary>
[StringValue]
public partial record ValidatedPlainTextPassword
{
    internal string Value { get; }

    /// <summary>
    /// The user this password was validated for. Write operations will reject this password
    /// if it is applied to a different user. Null when loaded from storage (not validated).
    /// </summary>
    public UserSubjectId? ValidatedForUserId { get; }

    /// <summary>
    /// Constructs a <see cref="ValidatedPlainTextPassword"/> from an already validated password string.
    /// This should only be called by <see cref="ValidatedPlainTextPasswordFactory"/> after the string has been validated.
    /// </summary>
    internal ValidatedPlainTextPassword(string value, UserSubjectId validatedForUserId)
    {
        Value = value;
        ValidatedForUserId = validatedForUserId;
    }

    // Used by the source generator's Load method
    private ValidatedPlainTextPassword(string value)
    {
        Value = value;
        ValidatedForUserId = null;
    }

    /// <summary>Returns a redacted string to prevent accidental logging of password values.</summary>
    public override string ToString() => GetType().ToString();

    /// <summary>
    /// Implicitly converts a <see cref="ValidatedPlainTextPassword"/> to a <see cref="NonValidatedPassword"/>
    /// for use in authentication operations.
    /// </summary>
    /// <param name="password">The plain text password to convert.</param>
    public static implicit operator NonValidatedPassword(ValidatedPlainTextPassword password)
    {
        ArgumentNullException.ThrowIfNull(password);
        return password.ToNonValidatedPassword();
    }

    /// <summary>
    /// Convert the plain text password to a non-validated password, so it can be used
    /// for authentication. 
    /// </summary>
    /// <returns></returns>
    public NonValidatedPassword ToNonValidatedPassword() => NonValidatedPassword.Create(Value);
}
