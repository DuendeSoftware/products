// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Membership;

/// <summary>
/// DTO for listing members of a group via membership.
/// </summary>
public sealed record MembershipGroupMemberListItem
{
    /// <summary>
    /// The subject identifier of the member.
    /// </summary>
    public required UserSubjectId SubjectId { get; init; }
}
