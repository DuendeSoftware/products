// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Http;

namespace Duende.Bff.DynamicFrontends.Internal;

internal class PathMappingMiddleware(RequestDelegate next, SelectedFrontend selectedFrontend, PathMapper pathMapper)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (selectedFrontend.TryGet(out var frontend))
        {
            pathMapper.MapPath(context, frontend);
        }

        await next(context);
    }
}
