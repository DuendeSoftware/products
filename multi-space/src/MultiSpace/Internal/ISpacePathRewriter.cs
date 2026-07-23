// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Http;

namespace Duende.MultiSpace.Internal;

internal interface ISpacePathRewriter
{
    /// <summary>
    /// Rewrites the request path when a path-based space match was found.
    /// Moves the matched path into PathBase and sets Path to the remaining portion.
    /// Returns false if rewriting fails (matched path not found at start of request path).
    /// </summary>
    bool TryRewrite(HttpContext context, string matchedPath);
}
