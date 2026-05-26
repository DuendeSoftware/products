// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net.Mime;
using Microsoft.AspNetCore.Http;

namespace Duende.UserManagement.Scim.Internal;

#pragma warning disable CA1812 // Avoid uninstantiated internal classes
internal sealed class ScimContentTypeFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        // Validate incoming content-type only for methods that carry a request body.
        var request = context.HttpContext.Request;
        if (HttpMethods.IsPost(request.Method) ||
            HttpMethods.IsPut(request.Method) ||
            HttpMethods.IsPatch(request.Method))
        {
            var contentType = request.ContentType;
            if (contentType is not null &&
                !contentType.StartsWith(ScimConstants.ScimContentType, StringComparison.OrdinalIgnoreCase) &&
                !contentType.StartsWith(MediaTypeNames.Application.Json, StringComparison.OrdinalIgnoreCase))
            {
                var errorResult = ScimResults.Error(
                    415,
                    detail: $"Unsupported content type '{contentType}'. Use 'application/scim+json' or 'application/json'.");
                return errorResult;
            }
        }

        return await next(context);
    }
}
