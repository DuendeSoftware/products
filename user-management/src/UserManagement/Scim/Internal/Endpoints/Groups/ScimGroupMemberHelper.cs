// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage;
using Duende.Storage.Pagination;
using Duende.UserManagement.Membership;
using Duende.UserManagement.Membership.Internal.Storage;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Duende.UserManagement.Scim.Internal.Endpoints.Groups;

/// <summary>
/// Shared helper for fetching group members with truncation and warning logging.
/// </summary>
internal static partial class ScimGroupMemberHelper
{
    /// <summary>
    /// Fetches group members up to the configured limit, mapping each to a <see cref="ScimGroupMember"/>.
    /// Logs a warning when the total member count exceeds the limit.
    /// </summary>
    internal static async Task<IReadOnlyList<ScimGroupMember>> FetchMembersWithTruncationAsync(
        IMembershipAdmin membershipAdmin,
        GroupId groupId,
        int maxMembers,
        string baseUrl,
        string usersRoute,
        ILogger logger,
        Ct ct)
    {
        if (maxMembers <= 0)
        {
            return [];
        }

        var clampedMax = Math.Min(maxMembers, 200);

        var membersResult = await membershipAdmin.GetMembersInGroupAsync(
            groupId,
            DataRange.FromPage(1, clampedMax),
            ct);

        if (membersResult.TotalCount is > 0 and var total && total > clampedMax)
        {
            LogMembersTruncated(logger, groupId, total, clampedMax);
        }

        return membersResult.Items
            .Select(m => ScimGroupMapper.MapToMember(m, baseUrl, usersRoute))
            .ToList();
    }

    /// <summary>
    /// Pages through all members of a group using cursor-based pagination
    /// and returns their subject IDs.
    /// </summary>
    internal static async Task<HashSet<UserSubjectId>> GetAllMemberIdsAsync(
        IMembershipAdmin membershipAdmin,
        GroupId groupId,
        Ct ct)
    {
        const int pageSize = 200;
        var allIds = new HashSet<UserSubjectId>();
        var pageNumber = 1;

        while (true)
        {
            var result = await membershipAdmin.GetMembersInGroupAsync(
                groupId, DataRange.FromPage(pageNumber, pageSize), ct);

            foreach (var member in result.Items)
            {
                _ = allIds.Add(member.SubjectId);
            }

            if (!result.HasMoreData)
            {
                break;
            }

            pageNumber++;
        }

        return allIds;
    }

    /// <summary>
    /// Parses raw SCIM member ID strings into <see cref="UserSubjectId"/> values.
    /// Returns an error result if any value is not a valid GUID.
    /// </summary>
    internal static (List<UserSubjectId>? Parsed, IResult? Error) ParseMemberSubjectIds(IReadOnlyList<string>? memberIds)
    {
        if (memberIds is not { Count: > 0 })
        {
            return ([], null);
        }

        var subjectIds = new List<UserSubjectId>(memberIds.Count);
        var invalid = new List<string>();
        foreach (var memberId in memberIds)
        {
            if (Guid.TryParse(memberId, out var memberGuid))
            {
                subjectIds.Add(UserSubjectId.Create(memberGuid.ToString()));
            }
            else
            {
                invalid.Add(memberId);
            }
        }

        if (invalid.Count > 0)
        {
            var ids = string.Join(", ", invalid);
            var error = ScimResults.Error(400, ScimConstants.ErrorTypes.InvalidValue,
                $"Invalid member value(s): {ids}. Member values must be valid user IDs (GUIDs).");
            return (null, error);
        }

        return (subjectIds, null);
    }

    /// <summary>
    /// Resolves member subject IDs to their store UUIDs, auto-creating memberships as needed.
    /// </summary>
    internal static async Task<(List<UuidV7>? Resolved, IResult? Error)> ResolveAndValidateMemberUuidsAsync(
        MembershipRepository membershipRepository,
        IReadOnlyList<UserSubjectId> subjectIds,
        Ct ct)
    {
        var seen = new HashSet<UserSubjectId>();
        var resolved = new List<UuidV7>(subjectIds.Count);
        foreach (var subjectId in subjectIds)
        {
            if (!seen.Add(subjectId))
            {
                continue;
            }

            var userUuid = await membershipRepository.GetOrCreateUserUuidAsync(subjectId, ct);
            resolved.Add(userUuid);
        }

        return (resolved, null);
    }

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Group {GroupId} has {TotalCount} members but only {Limit} are returned in the response. " +
                  "Configure ScimOptions.MaxGroupMembersInResponse to adjust this limit, or use excludedAttributes=members to exclude the members array.")]
    private static partial void LogMembersTruncated(ILogger logger, GroupId groupId, int totalCount, int limit);
}
