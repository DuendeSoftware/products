// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.Builder;
using Duende.Bff.Configuration;
using Duende.Bff.Yarp.Internal;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Yarp.ReverseProxy.Configuration;

namespace Duende.Bff.Yarp;

internal class ServiceProviderKeys
{
    internal const string ProxyConfigurationKey = "Duende.BFF.Configuration.ProxyConfiguration";
}

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
    public static T AddRemoteApis<T>(this T builder) where T : IBffServicesBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.RegisterConfigurationLoader((services, config) =>
        {
            // This line is commented out because of issue:
            // https://github.com/dotnet/runtime/issues/119883
            //services.Configure<ProxyConfiguration>(config);

            // As a workaround, we're registering the config as a singleton
            // then loading the singleton when the config reloads. 
            services.AddKeyedSingleton(ServiceProviderKeys.ProxyConfigurationKey, config);

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

    public static IReverseProxyBuilder AddYarpConfig(this IBffServicesBuilder builder, RouteConfig[] routes,
        ClusterConfig[] clusters)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var yarpBuilder = builder.Services.AddReverseProxy()
            .AddBffExtensions();

        yarpBuilder.LoadFromMemory(routes, clusters);

        return yarpBuilder;
    }

    public static IReverseProxyBuilder AddYarpConfig(this IBffServicesBuilder builder, IConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var yarpBuilder = builder.Services.AddReverseProxy()
            .AddBffExtensions();

        yarpBuilder.LoadFromConfig(config);

        return yarpBuilder;
    }

    public static IApplicationBuilder UseBffRemoteRoutes(this IApplicationBuilder app) => app.UseMiddleware<MapRemoteRoutesMiddleware>();

}
