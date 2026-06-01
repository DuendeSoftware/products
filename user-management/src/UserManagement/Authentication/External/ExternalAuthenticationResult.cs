// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Authentication.External;

/// <summary>
/// Represents the result of resolving an external authenticator address to a user.
/// </summary>
public abstract record ExternalAuthenticationResult
{
    private ExternalAuthenticationResult() { }

    /// <summary>
    /// The resolution succeeded. Contains the resolved user's subject ID.
    /// </summary>
    public sealed record Success : ExternalAuthenticationResult
    {
        internal Success(UserSubjectId userSubjectId) => UserSubjectId = userSubjectId;

        /// <summary>The subject ID of the resolved user.</summary>
        public UserSubjectId UserSubjectId { get; }
    }

    /// <summary>
    /// The resolution failed.
    /// </summary>
    public sealed record Failure : ExternalAuthenticationResult
    {
        internal static Failure Instance { get; } = new();

        private Failure() { }
    }
}
