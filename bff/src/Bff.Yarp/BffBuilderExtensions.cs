// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.DynamicFrontends.Internal;
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
        builder.Services.AddHttpForwarder();
        builder.Services.AddSingleton<IRemoteRouteHandler, RemoteRouteHandler>();

        return builder;
    }

    public static IReverseProxyBuilder AddYarpConfig(this BffBuilder builder, RouteConfig[] routes,
        ClusterConfig[] clusters)
    {
        var yarpBuilder = builder.Services.AddReverseProxy()
            .AddBffExtensions();

        yarpBuilder.LoadFromMemory(routes, clusters);

        return yarpBuilder;
    }

    public static IReverseProxyBuilder AddYarpConfig(this BffBuilder builder, IConfiguration config)
    {
        var yarpBuilder = builder.Services.AddReverseProxy()
            .AddBffExtensions();

        yarpBuilder.LoadFromConfig(config);

        return yarpBuilder;
    }
}
