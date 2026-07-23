// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Http;

namespace Duende.MultiSpace.Internal;

internal sealed class DefaultSpacePathRewriter : ISpacePathRewriter
{
    /// <inheritdoc/>
    public bool TryRewrite(HttpContext context, string matchedPath)
    {
        if (!context.Request.Path.StartsWithSegments(matchedPath, StringComparison.OrdinalIgnoreCase, out var remaining))
        {
            return false;
        }

        context.Request.PathBase = context.Request.PathBase.Add(matchedPath);
        context.Request.Path = remaining.HasValue ? remaining : new PathString("/");
        return true;
    }
}
