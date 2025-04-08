// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.DynamicFrontends.Internal;
using Duende.Bff.Endpoints.Internal;
using Microsoft.AspNetCore.Builder;

namespace Duende.Bff;

/// <summary>
/// Extension methods for the BFF middleware
/// </summary>
public static class BffApplicationBuilderExtensions
{
    /// <summary>
    /// Adds the Duende.BFF middlewares to the pipeline
    /// </summary>
    /// <returns></returns>
    public static IApplicationBuilder UseBff(this IApplicationBuilder app) => app.UseMiddleware<BffAntiForgeryMiddleware>();

    public static IApplicationBuilder UseBffAntiForgery(this IApplicationBuilder app)
    {
        app.Properties[Constants.AspnetCorePipeline.AntiForgeryAdded] = true;
        return app.UseMiddleware<BffAntiForgeryMiddleware>();
    }

    public static IApplicationBuilder UseBffFrontendSelection(this IApplicationBuilder app)
    {
        app.Properties[Constants.AspnetCorePipeline.FrontendSelectionAdded] = true;
        return app.UseMiddleware<FrontendSelectionMiddleware>();
    }

    public static IApplicationBuilder UseBffPathMapping(this IApplicationBuilder app)
    {
        app.Properties[Constants.AspnetCorePipeline.PathMappingAdded] = true;
        return app.UseMiddleware<PathMappingMiddleware>();
    }

    public static IApplicationBuilder UseBffOpenIdCallbacks(this IApplicationBuilder app)
    {
        app.Properties[Constants.AspnetCorePipeline.OpenIdCallbacksAdded] = true;
        return app.UseMiddleware<OpenIdConnectCallbackMiddleware>();
    }

    public static IApplicationBuilder UseBffRemoteRoutes(this IApplicationBuilder app)
    {
        app.Properties[Constants.AspnetCorePipeline.RemoteRoutesAdded] = true;
        return app.UseMiddleware<MapRemoteRoutesMiddleware>();
    }

    public static IApplicationBuilder UseBffIndexPages(this IApplicationBuilder app)
    {
        app.Properties[Constants.AspnetCorePipeline.BffIndexPagesAdded] = true;
        return app.UseMiddleware<ProxyIndexMiddleware>();
    }
}
