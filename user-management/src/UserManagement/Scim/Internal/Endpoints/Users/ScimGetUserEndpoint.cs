// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.UserManagement.Internal.Services;
using Duende.UserManagement.Profiles.Internal.Storage;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Duende.UserManagement.Scim.Internal.Endpoints.Users;

#pragma warning disable CA1812 // Avoid uninstantiated internal classes
internal sealed class ScimGetUserEndpoint(
    UserProfileRepository profileRepo,
    IServerUrls serverUrls,
    IOptions<ScimEndpointOptions> scimOptions,
    ILogger<ScimGetUserEndpoint> logger)
{
    internal async Task<IResult> HandleAsync(
        string id,
        HttpContext context,
        [FromQuery] string? attributes,
        [FromQuery] string? excludedAttributes,
        Ct ct)
    {
        if (!Guid.TryParse(id, out var guid))
        {
            logger.ScimGetUserNotFound(LogLevel.Information, id);
            return ScimResults.Error(404, detail: "User not found.");
        }

        var subjectId = UserSubjectId.Create(guid.ToString());

        // Profile is the sole source of user existence
        var profileResult = await profileRepo.TryReadAsync(subjectId, ct);
        if (profileResult is null)
        {
            logger.ScimGetUserNotFound(LogLevel.Information, id);
            return ScimResults.Error(404, detail: "User not found.");
        }

        var (profile, version) = profileResult.Value;

        // Check If-None-Match header for 304
        var notModified = ScimEndpointHelpers.CheckIfNoneMatch(context, version);
        if (notModified is not null)
        {
            return notModified;
        }

        var baseUrl = serverUrls.BaseUrl;
        var resource = ScimUserMapper.MapToResource(profile, version, baseUrl, scimOptions.Value.Route);

        // Apply attribute projection
        var attributeSet = ScimEndpointHelpers.ParseAttributeSet(attributes);
        var excludedAttributeSet = ScimEndpointHelpers.ParseAttributeSet(excludedAttributes);
        resource = ScimAttributeProjection.Apply(resource, attributeSet, excludedAttributeSet);

        // Set ETag header
        context.Response.Headers.ETag = ((ScimETag)version).ToHeaderValue();

        return ScimResults.Ok(resource);
    }
}
