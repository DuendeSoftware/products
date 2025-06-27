// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Http;

namespace Duende.Bff.DynamicFrontends.Internal;

internal static class PathMapper
{
    public static void MapPath(HttpContext context, BffFrontend frontend)
    {
        var path = frontend.SelectionCriteria.MatchingPath;

        if (path == null)
        {
            return;
        }

        if (!context.Request.Path.StartsWithSegments(path, StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = 404;
            return;
        }

        // add the current path to the pathbase and remove it from the path
        context.Request.PathBase = context.Request.PathBase.Add(path);
        context.Request.Path = context.Request.Path.Value?.Substring(path.Length);
    }
}
