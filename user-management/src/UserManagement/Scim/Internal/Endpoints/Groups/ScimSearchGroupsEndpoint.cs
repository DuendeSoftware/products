// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Pagination;
using Duende.Storage.Querying;
using Duende.UserManagement.Internal.Services;
using Duende.UserManagement.Membership;
using Duende.UserManagement.Scim.Internal.Endpoints.Users;
using Duende.UserManagement.Scim.Internal.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Duende.UserManagement.Scim.Internal.Endpoints.Groups;

#pragma warning disable CA1812 // Avoid uninstantiated internal classes
internal sealed class ScimSearchGroupsEndpoint(
    IGroupAdmin groupAdmin,
    IMembershipAdmin membershipAdmin,
    IServerUrls serverUrls,
    IOptions<ScimEndpointOptions> scimOptions,
    IOptions<ScimOptions> options,
    ILogger<ScimSearchGroupsEndpoint> logger)
{
    internal async Task<IResult> HandleAsync(
        ScimSearchRequest? body,
        HttpContext context,
        Ct ct)
    {
        var filter = body?.Filter;
        var resolvedStartIndex = Math.Max(1, body?.StartIndex ?? 1);
        var resolvedCount = body?.Count ?? 20;
        var pageSize = Math.Clamp(resolvedCount, 1, Math.Min(options.Value.MaxResults, 200));
        var sortDirection = ScimEndpointHelpers.ParseSortDirection(body?.SortOrder);
        var baseUrl = serverUrls.BaseUrl;

        var groupFilter = new GroupFilter();
        if (!string.IsNullOrWhiteSpace(filter))
        {
            if (!SearchExpression.TryCreate(filter, out var searchExpression))
            {
                return ScimResults.Error(400, ScimConstants.ErrorTypes.InvalidFilter,
                    "Invalid filter expression.");
            }

            groupFilter = groupFilter with { SearchExpression = searchExpression };
        }

        var sort = ScimGroupSortHelper.MapSortBy(body?.SortBy, sortDirection);

        // Convert SCIM 1-based startIndex to offset-based pagination
        var skip = Math.Max(0, resolvedStartIndex - 1);
        var page = DataRange.FromOffset(skip, pageSize);

        QueryResult<GroupListItem> queryResult;
        try
        {
            queryResult = await groupAdmin.QueryAsync(QueryRequest.Create(groupFilter, sort, page), ct);
        }
        catch (FilterParseException ex)
        {
            logger.ScimFilterParseFailure(LogLevel.Information, ScimConstants.ResourceTypes.Group, ex.Message);
            return ScimResults.Error(400, ScimConstants.ErrorTypes.InvalidFilter,
                $"Invalid filter expression: {ex.Message}");
        }
        catch (NotSupportedException ex)
        {
            logger.ScimFilterParseFailure(LogLevel.Information, ScimConstants.ResourceTypes.Group, ex.Message);
            return ScimResults.Error(400, ScimConstants.ErrorTypes.InvalidFilter, ex.Message);
        }

        // Members are excluded from search responses by default.
        // Include them only when explicitly requested via the attributes parameter,
        // and do not load them when explicitly excluded via excludedAttributes.
        var attributeSet = ScimEndpointHelpers.ParseAttributeSet(body?.Attributes);
        var excludedAttributeSet = ScimEndpointHelpers.ParseAttributeSet(body?.ExcludedAttributes);
        var membersExcluded = excludedAttributeSet?.Contains(ScimConstants.Attributes.Members) == true;
        var includeMembers = !membersExcluded && attributeSet?.Contains(ScimConstants.Attributes.Members) == true;

        var routePrefix = scimOptions.Value.GroupsRoute;
        var resources = new List<ScimGroupResource>(queryResult.Items.Count);
        foreach (var item in queryResult.Items)
        {
            IReadOnlyList<ScimGroupMember>? members = null;
            if (includeMembers)
            {
                var fetched = await ScimGroupMemberHelper.FetchMembersWithTruncationAsync(
                    membershipAdmin, item.Id, options.Value.MaxGroupMembersInResponse,
                    baseUrl, scimOptions.Value.Route, logger, ct);
                members = fetched.Count > 0 ? fetched : null;
            }

            resources.Add(ScimGroupAttributeProjection.Apply(
                ScimGroupMapper.MapToResource(item, members, baseUrl, routePrefix),
                attributeSet, excludedAttributeSet));
        }

        var listResponse = new ScimListResponse<ScimGroupResource>
        {
            TotalResults = queryResult.TotalCount ?? 0,
            StartIndex = resolvedStartIndex,
            ItemsPerPage = resources.Count,
            Resources = resources
        };

        return ScimResults.Ok(listResponse);
    }
}
