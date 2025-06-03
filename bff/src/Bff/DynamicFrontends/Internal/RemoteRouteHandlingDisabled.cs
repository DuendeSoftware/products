// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Duende.Bff.DynamicFrontends.Internal;

internal class RemoteRouteHandlingDisabled(ILogger<RemoteRouteHandlingDisabled> logger) : IRemoteRouteHandler
{
    public Task<bool> HandleAsync(HttpContext context, CT ct = default)
    {
        logger.LogWarning("BFF.Yarp is not registered, so remote route handling is disabled. ");
        context.Response.StatusCode = (int)HttpStatusCode.NotFound;
        return Task.FromResult(false);
    }
}
