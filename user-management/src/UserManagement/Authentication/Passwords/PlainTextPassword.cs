// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.UserManagement.Authentication.Passwords.Internal;

namespace Duende.UserManagement.Authentication.Passwords;

[StringValue]
public partial record PlainTextPassword
{
    internal string Value { get; }

    /// <summary>
    /// The user this password was validated for. Write operations will reject this password
    /// if it is applied to a different user. Null when loaded from storage (not validated).
    /// </summary>
    public UserSubjectId? ValidatedForUserId { get; }

    /// <summary>
    /// Constructs a <see cref="PlainTextPassword"/> from an already validated password string.
    /// This should only be called by <see cref="PlainTextPasswordFactory"/> after the string has been validated.
    /// </summary>
    internal PlainTextPassword(string value, UserSubjectId validatedForUserId)
    {
        Value = value;
        ValidatedForUserId = validatedForUserId;
    }

    // Used by the source generator's Load method
    private PlainTextPassword(string value)
    {
        Value = value;
        ValidatedForUserId = null;
    }

    public override string ToString() => GetType().ToString();

    public static implicit operator NonValidatedPassword(PlainTextPassword password)
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
