// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.UserManagement.Internal.Services;
using Duende.UserManagement.Membership;
using Duende.UserManagement.Scim.Internal.Endpoints.Users;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Duende.UserManagement.Scim.Internal.Endpoints.Groups;

#pragma warning disable CA1812 // Avoid uninstantiated internal classes
internal sealed class ScimGetGroupEndpoint(
    IGroupAdmin groupAdmin,
    IMembershipAdmin membershipAdmin,
    IServerUrls serverUrls,
    IOptions<ScimEndpointOptions> scimOptions,
    IOptions<ScimOptions> options,
    ILogger<ScimGetGroupEndpoint> logger)
{
    internal async Task<IResult> HandleAsync(
        string id,
        HttpContext context,
        [FromQuery] string? attributes,
        [FromQuery] string? excludedAttributes,
        Ct ct)
    {
        if (!GroupId.TryCreate(id, out var parsedGroupId))
        {
            return ScimResults.Error(404, detail: "Group not found.");
        }

        var groupId = parsedGroupId;

        var result = await groupAdmin.GetAsync(groupId, ct);
        if (!result.Found)
        {
            logger.ScimGetGroupNotFound(LogLevel.Information, id);
            return ScimResults.Error(404, detail: "Group not found.");
        }

        var version = result.Version.Value;

        // Check If-None-Match header for 304
        var notModified = ScimEndpointHelpers.CheckIfNoneMatch(context, version);
        if (notModified is not null)
        {
            return notModified;
        }

        // Determine whether to load members (skip if excluded)
        var excludedAttributeSet = ScimEndpointHelpers.ParseAttributeSet(excludedAttributes);
        var attributeSet = ScimEndpointHelpers.ParseAttributeSet(attributes);
        var membersExcluded = excludedAttributeSet?.Contains(ScimConstants.Attributes.Members) == true;
        var membersIncluded = attributeSet is null || attributeSet.Contains(ScimConstants.Attributes.Members);

        IReadOnlyList<ScimGroupMember>? members = null;
        if (!membersExcluded && membersIncluded)
        {
            var fetchedMembers = await ScimGroupMemberHelper.FetchMembersWithTruncationAsync(
                membershipAdmin, groupId, options.Value.MaxGroupMembersInResponse,
                serverUrls.BaseUrl, scimOptions.Value.Route, logger, ct);
            members = fetchedMembers.Count > 0 ? fetchedMembers : null;
        }

        var groupListDto = new GroupListItem
        {
            Id = groupId,
            Name = result.Item.Name,
            Description = result.Item.Description
        };

        var resource = ScimGroupMapper.MapToResource(
            groupListDto,
            members,
            version,
            serverUrls.BaseUrl,
            scimOptions.Value.GroupsRoute);

        // Apply attribute projection
        resource = ScimGroupAttributeProjection.Apply(resource, attributeSet, excludedAttributeSet);

        // Set ETag header
        context.Response.Headers.ETag = ((ScimETag)version).ToHeaderValue();

        return ScimResults.Ok(resource);
    }
}
