// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.EntityAttributeValue;

namespace Duende.UserManagement.Authentication.Passwords;

/// <summary>
/// Provides password-based authentication for users.
/// </summary>
public interface IPasswordAuth
{
    /// <summary>
    /// Attempts to authenticate a user with the given attribute identifier and password.
    /// </summary>
    /// <param name="code">The attribute code identifying the user (e.g., email).</param>
    /// <param name="value">The attribute value to match.</param>
    /// <param name="password">The password supplied by the user (not yet validated against policy).</param>
    /// <param name="ct">Cancellation token.</param>
    Task<PasswordAuthenticationResult> TryAuthenticateAsync(
        AttributeCode code, object value, NonValidatedPassword password, Ct ct);
}
