// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.EntityAttributeValue;

namespace Duende.UserManagement.Import;

/// <summary>
/// Represents a single user to be imported. Any combination of
/// <see cref="ProfileAttributes"/>, <see cref="Authenticators"/>, and
/// <see cref="Memberships"/> may be provided.
/// </summary>
public sealed record UserImportRecord
{
    /// <summary>The subject ID for the user.</summary>
    public required UserSubjectId SubjectId { get; init; }

    /// <summary>
    /// Profile attributes to import. When null, no profile is created or updated.
    /// </summary>
    public ValidatedAttributeValueCollection? ProfileAttributes { get; init; }

    /// <summary>
    /// Authenticators to import. When null, no authenticators are created or updated.
    /// </summary>
    public AuthenticatorImport? Authenticators { get; init; }

    /// <summary>
    /// Group and role memberships to import. When null, no memberships are created or updated.
    /// </summary>
    public MembershipImport? Memberships { get; init; }
}
