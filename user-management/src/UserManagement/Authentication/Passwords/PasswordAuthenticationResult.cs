// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Authentication.Passwords;

/// <summary>
/// Discriminated union representing the outcome of a password authentication attempt.
/// </summary>
public abstract record PasswordAuthenticationResult
{
    private PasswordAuthenticationResult() { }

    /// <summary>
    /// Indicates a successful password authentication.
    /// </summary>
    public sealed record Success : PasswordAuthenticationResult
    {
        internal Success(UserSubjectId userSubjectId) => UserSubjectId = userSubjectId;

        /// <summary>Gets the subject ID of the authenticated user.</summary>
        public UserSubjectId UserSubjectId { get; }
    }

    /// <summary>
    /// Indicates that authentication succeeded but the user's password has expired and must be changed.
    /// </summary>
    public sealed record Expired : PasswordAuthenticationResult
    {
        internal Expired(UserSubjectId userSubjectId) => UserSubjectId = userSubjectId;

        /// <summary>Gets the subject ID of the user whose password has expired.</summary>
        public UserSubjectId UserSubjectId { get; }
    }

    /// <summary>
    /// Indicates a failed password authentication attempt.
    /// </summary>
    public sealed record Failure : PasswordAuthenticationResult
    {
        internal static Failure Instance { get; } = new();

        private Failure() { }
    }
}
