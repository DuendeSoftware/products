// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Yarp.ReverseProxy.Transforms.Builder;

namespace Duende.Bff.Yarp;

/// <summary>
/// Extension methods for the BFF endpoints
/// </summary>
public static class RouteBuilderExtensions
{
    /// <summary>
    /// Adds a remote BFF API endpoint
    /// </summary>
    /// <param name="endpoints"></param>
    /// <param name="localPath"></param>
    /// <param name="apiAddress"></param>
    /// <param name="yarpTransformBuilder"></param>
    /// <returns></returns>
    public static IEndpointConventionBuilder MapRemoteBffApiEndpoint(
        this IEndpointRouteBuilder endpoints,
        PathString localPath,
        string apiAddress,
        Action<TransformBuilderContext>? yarpTransformBuilder = null)
    {
        endpoints.CheckLicense();

        // Configure the yarp transform pipeline. Either use the one provided or the default
        yarpTransformBuilder ??= context =>
        {
            // For the default, either get one from DI (to globally configure a default) 
            var defaultYarpTransformBuilder = context.Services.GetService<BffYarpTransformBuilder>()
                // or use the built-in default
                ?? DefaultBffYarpTransformerBuilders.DirectProxyWithAccessToken;

            // invoke the default transform builder
            defaultYarpTransformBuilder(localPath, context);
        };

        return endpoints.MapForwarder(
                pattern: localPath.Add("/{**catch-all}").Value!,
                destinationPrefix: apiAddress,
                configureTransform: context =>
                {
                    yarpTransformBuilder(context);
                })
            .WithMetadata(new BffRemoteApiEndpointMetadata());
    }
}
