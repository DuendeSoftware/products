// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Hosting;

internal static class EndpointHelpers
{
    public static class OAuth2AuthorizationServerMetadataHelpers
    {
        public static bool OnRouteMatched(HttpContext context, RouteValueDictionary routeValues, ILogger logger)
        {
            if (context.Request.PathBase.Value.IsPresent())
            {
                logger.LogDebug("Path must start with .well-known, but request path base is set to '{PathBase}'", context.Request.PathBase.Value.SanitizeLogParameter());
                return false;
            }

            if (!routeValues.TryGetValue("subPath", out var subPath) || subPath == null)
            {
                return true;
            }

            var serverUrls = context.RequestServices.GetRequiredService<IServerUrls>();
            serverUrls.BasePath = subPath.ToString().EnsureLeadingSlash();

            return true;
        }
    }
}
