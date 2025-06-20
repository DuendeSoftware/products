// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.Configuration;
using Duende.Bff.Yarp.Internal;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Yarp.ReverseProxy.Configuration;

namespace Duende.Bff.Yarp;

/// <summary>
/// YARP related DI extension methods
/// </summary>
public static class BffBuilderExtensions
{
    /// <summary>
    /// Adds the services required for the YARP HTTP forwarder
    /// </summary>
    /// <param name="builder"></param>
    /// <returns></returns>
    public static BffBuilder AddRemoteApis(this BffBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.RegisterConfigurationLoader((services, config) =>
        {
            services.Configure<ProxyConfiguration>(config);
        });

        builder.Services.Configure<BffOptions>(opt =>
        {
            opt.MiddlewareLoaders.Add(app =>
            {
                app.UseBffRemoteRoutes();
            });
        });
        builder.Services.AddHttpForwarder();
        builder.Services.AddSingleton<RemoteRouteHandler>();

        builder.Services.AddSingleton<IBffPluginLoader, ProxyBffPluginLoader>();

        return builder;
    }

    public static IReverseProxyBuilder AddYarpConfig(this BffBuilder builder, RouteConfig[] routes,
        ClusterConfig[] clusters)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var yarpBuilder = builder.Services.AddReverseProxy()
            .AddBffExtensions();

        yarpBuilder.LoadFromMemory(routes, clusters);

        return yarpBuilder;
    }

    public static IReverseProxyBuilder AddYarpConfig(this BffBuilder builder, IConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var yarpBuilder = builder.Services.AddReverseProxy()
            .AddBffExtensions();

        yarpBuilder.LoadFromConfig(config);

        return yarpBuilder;
    }

    public static IApplicationBuilder UseBffRemoteRoutes(this IApplicationBuilder app) => app.UseMiddleware<MapRemoteRoutesMiddleware>();

}
