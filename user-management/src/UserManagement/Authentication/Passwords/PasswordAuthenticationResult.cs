// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Authentication.Passwords;

public abstract record PasswordAuthenticationResult
{
    private PasswordAuthenticationResult() { }

    public sealed record Success : PasswordAuthenticationResult
    {
        internal Success(UserSubjectId userSubjectId) => UserSubjectId = userSubjectId;

        public UserSubjectId UserSubjectId { get; }
    }

    public sealed record Expired : PasswordAuthenticationResult
    {
        internal Expired(UserSubjectId userSubjectId) => UserSubjectId = userSubjectId;

        public UserSubjectId UserSubjectId { get; }
    }

    public sealed record Failure : PasswordAuthenticationResult
    {
        internal static Failure Instance { get; } = new();

        private Failure() { }
    }
}
