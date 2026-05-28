// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Pagination;
using Duende.Storage.Querying;
using Duende.UserManagement.Internal.Licensing;
using Duende.UserManagement.Internal.Services;
using Duende.UserManagement.Profiles;
using Duende.UserManagement.Profiles.Internal.Storage;
using Duende.UserManagement.Scim.Internal.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Duende.UserManagement.Scim.Internal.Endpoints.Users;

#pragma warning disable CA1812 // Avoid uninstantiated internal classes
internal sealed class ScimSearchUsersEndpoint(
    UserProfileReader profileReader,
    IServerUrls serverUrls,
    IOptions<ScimEndpointOptions> scimOptions,
    UserManagementLicenseValidator licenseValidator,
    ILogger<ScimSearchUsersEndpoint> logger)
{
    internal async Task<IResult> HandleAsync(
        ScimSearchRequest? body,
        HttpContext context,
        Ct ct)
    {
        licenseValidator.ValidateInboundScim();

        var filter = body?.Filter;
        var resolvedStartIndex = Math.Max(1, body?.StartIndex ?? 1);
        var resolvedCount = body?.Count ?? 20;
        var sortDirection = ScimEndpointHelpers.ParseSortDirection(body?.SortOrder);
        var baseUrl = serverUrls.BaseUrl;

        var attributeSet = ScimEndpointHelpers.ParseAttributeSet(body?.Attributes);
        var excludedAttributeSet = ScimEndpointHelpers.ParseAttributeSet(body?.ExcludedAttributes);

        UserProfileListItem[] items;
        int totalCount;
        try
        {
            var skip = Math.Max(0, resolvedStartIndex - 1);
            var clampedCount = Math.Clamp(resolvedCount, 1, 200);
            var dataRange = DataRange.FromOffset(skip, clampedCount);
            var queryResult = await profileReader.QueryAsync(filter, body?.SortBy, sortDirection, dataRange, ct);
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
