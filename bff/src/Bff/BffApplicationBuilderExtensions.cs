// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.Configuration;
using Duende.Bff.DynamicFrontends.Internal;
using Duende.Bff.Endpoints.Internal;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

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

    public static IApplicationBuilder UseBffAntiForgery(this IApplicationBuilder app) => app.UseMiddleware<BffAntiForgeryMiddleware>();

    public static IApplicationBuilder UseBffFrontendSelection(this IApplicationBuilder app) => app.UseMiddleware<FrontendSelectionMiddleware>();

    public static IApplicationBuilder UseBffPathMapping(this IApplicationBuilder app) => app.UseMiddleware<PathMappingMiddleware>();

    public static IApplicationBuilder UseBffOpenIdCallbacks(this IApplicationBuilder app) => app.UseMiddleware<OpenIdConnectCallbackMiddleware>();


    /// <summary>
    /// Registers the middleware that will proxy static file requests to the configured URLs for the current frontend.
    ///
    /// This is used to serve a frontend from a CDN or from a local development server. This middleware is automatically
    /// if <see cref="BffOptions.AutomaticallyRegisterBffMiddleware"/> is true (the default).
    /// 
    /// </summary>
    /// <param name="app"></param>
    /// <returns></returns>
    public static IApplicationBuilder UseBffStaticFileProxying(this IApplicationBuilder app) => app.UseMiddleware<ProxyStaticFilesMiddleware>();

    /// <summary>
    /// If you have disabled automatic middleware registration using <see cref="BffOptions.AutomaticallyRegisterBffMiddleware"/>
    /// Then you must call this very early in the aspnet core pipeline. It handles FrontendSelection, Path mapping and OpenID callbacks.
    /// If you have not disabled automatic middleware registration, then this is called automatically by the BFF framework.
    /// </summary>
    public static IApplicationBuilder UseBffPreProcessing(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.UseBffFrontendSelection();
        app.UseBffPathMapping();
        app.UseBffOpenIdCallbacks();
        return app;
    }

    /// <summary>
    /// If you have disabled automatic middleware registration using <see cref="BffOptions.AutomaticallyRegisterBffMiddleware"/>
    /// Then you must call at the end of the aspnet core pipeline. It adds the remote api handling, management endpoints and index pages.
    /// If you have not disabled automatic middleware registration, then this is called automatically by the BFF framework.
    /// </summary>
    /// <param name="app"></param>
    /// <returns></returns>
    public static IApplicationBuilder UseBffPostProcessing(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var bffOptions = app.ApplicationServices.GetRequiredService<IOptions<BffOptions>>()
            .Value;
        foreach (var loader in bffOptions.MiddlewareLoaders)
        {
            loader(app);
        }
        app.UseEndpoints(endpoints =>
        {
            // Mapping the management endpoints. 
            endpoints.MapBffManagementLoginEndpoint();
#pragma warning disable CS0618 // Type or member is obsolete
            endpoints.MapBffManagementSilentLoginEndpoints();
#pragma warning restore CS0618 // Type or member is obsolete
            endpoints.MapBffManagementLogoutEndpoint();
            endpoints.MapBffManagementUserEndpoint();
            endpoints.MapBffManagementBackchannelEndpoint();
            endpoints.MapBffDiagnosticsEndpoint();
        });
        app.UseBffStaticFileProxying();
        return app;

    }
}
