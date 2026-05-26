// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Import;

/// <summary>
/// The reason a conflict was detected during user import.
/// </summary>
public enum UserImportConflictReason
{
    /// <summary>A user profile with the same subject ID already exists.</summary>
    ProfileAlreadyExists,

    /// <summary>
    /// A unique attribute value (including username) on the incoming record
    /// already belongs to a different user profile.
    /// </summary>
    ProfileUniqueKeyConflict,

    /// <summary>Authenticators for the same subject ID already exist.</summary>
    AuthenticatorAlreadyExists,

    /// <summary>The username is already claimed by a different user's authenticators.</summary>
    AuthenticatorKeyConflict,

    /// <summary>An optimistic concurrency conflict occurred.</summary>
    ConcurrencyConflict,

    /// <summary>A membership record for the same subject ID already exists.</summary>
    MembershipAlreadyExists
}
