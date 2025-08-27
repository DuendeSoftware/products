// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Http;

namespace Duende.IdentityServer.Hosting;

internal static class EndpointHelpers
{
    public static class OAuthMetadataHelpers
    {
        public static bool IsMatch(HttpContext httpContext) => httpContext.Request.Path.StartsWithSegments("/.well-known/oauth-authorization-server",
                StringComparison.OrdinalIgnoreCase);
    }
}
