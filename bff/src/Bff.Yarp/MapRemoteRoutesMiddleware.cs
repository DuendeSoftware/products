// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.DynamicFrontends;
using Duende.Bff.Yarp.Internal;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.Bff.Yarp;

internal class MapRemoteRoutesMiddleware(RequestDelegate next, RemoteRouteHandler remoteRouteHandler)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (ShouldMapRemoteRoutes(context))
        {
            if (await remoteRouteHandler.HandleAsync(context, context.RequestAborted))
            {
               return;
            }
        }
        await next(context);
    }

    private bool ShouldMapRemoteRoutes(HttpContext context)
    {
        var selectedFrontend = context.RequestServices.GetRequiredService<SelectedFrontend>();

        if (!selectedFrontend.TryGet(out var frontend))
        {
            return false;
        }

        return frontend.GetRemoteApis().Any();
    }
}
