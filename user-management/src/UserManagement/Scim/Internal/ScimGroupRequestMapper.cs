// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.UserManagement.Membership;
using Duende.UserManagement.Scim.Internal.Endpoints.Groups;

namespace Duende.UserManagement.Scim.Internal;

/// <summary>
/// Maps SCIM Group request bodies to domain objects.
/// </summary>
internal static class ScimGroupRequestMapper
{
    /// <summary>
    /// Result of mapping a SCIM group request to domain objects.
    /// </summary>
    internal sealed class MappingResult
    {
        private MappingResult() { }

        internal bool IsSuccess { get; private set; }
        internal string? ErrorDetail { get; private set; }
        internal string? ErrorScimType { get; private set; }
        internal GroupName? GroupName { get; private set; }
        internal IReadOnlyList<string>? MemberIds { get; private set; }

        internal static MappingResult Success(GroupName groupName, IReadOnlyList<string>? memberIds) =>
            new() { IsSuccess = true, GroupName = groupName, MemberIds = memberIds };

        internal static MappingResult Failure(string detail) =>
            new() { IsSuccess = false, ErrorDetail = detail };

        internal static MappingResult Failure(string detail, string scimType) =>
            new() { IsSuccess = false, ErrorDetail = detail, ErrorScimType = scimType };
    }

    /// <summary>
    /// Maps a <see cref="ScimGroupRequest"/> to domain objects.
    /// Returns an error result if <c>displayName</c> is missing or invalid.
    /// </summary>
    internal static MappingResult Map(ScimGroupRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.DisplayName))
        {
            return MappingResult.Failure(
                "The 'displayName' attribute is required.",
                ScimConstants.ErrorTypes.InvalidValue);
        }

        if (!GroupName.TryCreate(request.DisplayName, out var groupName))
        {
            return MappingResult.Failure(
                $"The 'displayName' value '{request.DisplayName}' is not valid. " +
                "It must not exceed the maximum allowed length.",
                ScimConstants.ErrorTypes.InvalidValue);
        }

        var memberIds = request.Members
            ?.Where(m => !string.IsNullOrWhiteSpace(m.Value))
            .Select(m => m.Value!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return MappingResult.Success(groupName.Value, memberIds is { Count: > 0 } ? memberIds : null);
    }
}
