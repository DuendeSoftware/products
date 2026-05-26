// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Import;

/// <summary>
/// Identifies which step of the import encountered a conflict.
/// </summary>
public enum UserImportStep
{
    /// <summary>The user profile creation/update step.</summary>
    Profile,

    /// <summary>The authenticator creation/update step.</summary>
    Authenticator,

    /// <summary>The membership assignment step.</summary>
    Membership,

    /// <summary>The root user record creation step.</summary>
    UserRecord
}
