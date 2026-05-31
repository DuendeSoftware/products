// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.UserManagement.Authentication;
using Duende.UserManagement.Membership;
using Duende.UserManagement.Profiles;

namespace Duende.UserManagement;

/// <summary>
/// Provides administrative operations for managing users.
/// </summary>
public interface IUserAdmin
{
    /// <summary>
    /// Attempts to remove the specified user entirely.
    /// </summary>
    /// <param name="subjectId">The subject identifier of the user.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns><c>true</c> if the user was removed successfully; otherwise, <c>false</c>.</returns>
    Task<bool> TryRemoveAsync(UserSubjectId subjectId, Ct ct);

    /// <summary>
    /// Gets the membership administration service.
    /// </summary>
    IMembershipAdmin Membership { get; }

    /// <summary>
    /// Gets the user profile administration service.
    /// </summary>
    IUserProfileAdmin Profiles { get; }

    /// <summary>
    /// Gets the user authenticators administration service.
    /// </summary>
    IUserAuthenticatorsAdmin Authenticators { get; }

}
