// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Pagination;
using Duende.Storage.Querying;
using Duende.UserManagement.Internal.Services;
using Duende.UserManagement.Profiles;
using Duende.UserManagement.Profiles.Internal.Storage;
using Duende.UserManagement.Scim.Internal.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Duende.UserManagement.Scim.Internal.Endpoints.Users;

#pragma warning disable CA1812 // Avoid uninstantiated internal classes
internal sealed class ScimListUsersEndpoint(
    UserProfileReader profileReader,
    IServerUrls serverUrls,
    IOptions<ScimEndpointOptions> scimOptions,
    ILogger<ScimListUsersEndpoint> logger)
{
    internal async Task<IResult> HandleAsync(
        HttpContext context,
        [FromQuery] string? filter,
        [FromQuery] int? startIndex,
        [FromQuery] int? count,
        [FromQuery] string? sortBy,
        [FromQuery] string? sortOrder,
        [FromQuery] string? attributes,
        [FromQuery] string? excludedAttributes,
        Ct ct)
    {
        var resolvedStartIndex = Math.Max(1, startIndex ?? 1);
        var resolvedCount = count ?? 20;
        var sortDirection = ScimEndpointHelpers.ParseSortDirection(sortOrder);
        var baseUrl = serverUrls.BaseUrl;

        var attributeSet = ScimEndpointHelpers.ParseAttributeSet(attributes);
        var excludedAttributeSet = ScimEndpointHelpers.ParseAttributeSet(excludedAttributes);

        UserProfileListItem[] items;
        int totalCount;
        try
        {
            var skip = Math.Max(0, resolvedStartIndex - 1);
            var clampedCount = Math.Clamp(resolvedCount, 1, 200);
            var dataRange = DataRange.FromOffset(skip, clampedCount);
            var queryResult = await profileReader.QueryAsync(filter, sortBy, sortDirection, dataRange, ct);
            items = [.. queryResult.Items];
            totalCount = queryResult.TotalCount ?? 0;
        }
        catch (FilterParseException ex)
        {
            logger.ScimFilterParseFailure(LogLevel.Information, ScimConstants.ResourceTypes.User, ex.Message);
            return ScimResults.Error(400, ScimConstants.ErrorTypes.InvalidFilter,
                $"Invalid filter expression: {ex.Message}");
        }
        catch (NotSupportedException ex)
        {
            logger.ScimFilterParseFailure(LogLevel.Information, ScimConstants.ResourceTypes.User, ex.Message);
            return ScimResults.Error(400, ScimConstants.ErrorTypes.InvalidFilter, ex.Message);
        }

        var resources = items
            .Select(item =>
            {
                var resource = ScimUserMapper.MapToResource(item, baseUrl, scimOptions.Value.Route);
                return ScimAttributeProjection.Apply(resource, attributeSet, excludedAttributeSet);
            })
            .ToList();

        var listResponse = new ScimListResponse<ScimUserResource>
        {
            TotalResults = totalCount,
            StartIndex = resolvedStartIndex,
            ItemsPerPage = resources.Count,
            Resources = resources
        };

        return ScimResults.Ok(listResponse);
    }
}
