// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Http;

namespace Duende.Bff.DynamicFrontends.Internal;

internal class PathMappingMiddleware(RequestDelegate next, CurrentFrontendAccessor currentFrontendAccessor)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (currentFrontendAccessor.TryGet(out var frontend))
        {
            PathMapper.MapPath(context, frontend);
        }

        await next(context);
    }
}
