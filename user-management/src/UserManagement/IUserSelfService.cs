// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.UserManagement.Authentication;
using Duende.UserManagement.Profiles;

namespace Duende.UserManagement;

/// <summary>
/// Provides self-service operations that users can perform on their own accounts.
/// </summary>
public interface IUserSelfService
{
    /// <summary>
    /// Attempts to deregister (delete) the specified user's account.
    /// </summary>
    /// <param name="subjectId">The subject identifier of the user.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns><c>true</c> if the account was deregistered successfully; otherwise, <c>false</c>.</returns>
    Task<bool> TryDeregisterAsync(UserSubjectId subjectId, Ct ct);

    /// <summary>
    /// Gets the user profile self-service operations.
    /// </summary>
    IUserProfileSelfService Profiles { get; }

    /// <summary>
    /// Gets the user authenticators self-service operations.
    /// </summary>
    IUserAuthenticatorsSelfService Authenticators { get; }
}
