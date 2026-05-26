// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.UserManagement.Membership;
using Duende.UserManagement.Scim.Internal.Endpoints.Groups;
using Duende.UserManagement.Scim.Internal.Endpoints.Users;

namespace Duende.UserManagement.Scim.Internal;

/// <summary>
/// Maps between domain Group entities/DTOs and SCIM Group resources.
/// </summary>
internal static class ScimGroupMapper
{
    /// <summary>
    /// Maps a <see cref="GroupListItem"/> with version and optional members to a <see cref="ScimGroupResource"/>.
    /// </summary>
    internal static ScimGroupResource MapToResource(
        GroupListItem group,
        IReadOnlyList<ScimGroupMember>? members,
        int version,
        string baseUrl,
        string routePrefix)
    {
        var id = group.Id.Value;
        return new ScimGroupResource
        {
            Schemas = [ScimConstants.GroupSchemaUrn],
            Id = id,
            DisplayName = group.Name.Value,
            Members = members,
            Meta = new ScimMeta
            {
                ResourceType = ScimConstants.ResourceTypes.Group,
                Location = $"{baseUrl}{routePrefix}/{id}",
                Version = ((ScimETag)version).ToHeaderValue()
            }
        };
    }

    /// <summary>
    /// Maps a <see cref="GroupListItem"/> to a <see cref="ScimGroupResource"/> for list responses.
    /// Members are only included when explicitly requested via <c>?attributes=members</c>.
    /// </summary>
    internal static ScimGroupResource MapToResource(
        GroupListItem item,
        IReadOnlyList<ScimGroupMember>? members,
        string baseUrl,
        string routePrefix)
    {
        var id = item.Id.Value;
        return new ScimGroupResource
        {
            Schemas = [ScimConstants.GroupSchemaUrn],
            Id = id,
            DisplayName = item.Name.Value,
            Members = members,
            Meta = new ScimMeta
            {
                ResourceType = ScimConstants.ResourceTypes.Group,
                Location = $"{baseUrl}{routePrefix}/{id}",
                Version = null
            }
        };
    }

    /// <summary>
    /// Maps a <see cref="MembershipGroupMemberListItem"/> to a <see cref="ScimGroupMember"/>.
    /// </summary>
    internal static ScimGroupMember MapToMember(
        MembershipGroupMemberListItem member,
        string baseUrl,
        string usersRoutePrefix)
    {
        var memberId = member.SubjectId.Value;
        return new ScimGroupMember
        {
            Value = memberId,
            Ref = $"{baseUrl}{usersRoutePrefix}/{memberId}",
            Type = ScimConstants.ResourceTypes.User
        };
    }
}
