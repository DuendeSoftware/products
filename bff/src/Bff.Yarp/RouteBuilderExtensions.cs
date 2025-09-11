// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Yarp.ReverseProxy.Forwarder;
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
    /// <param name="endpoints">The endpoint route builder to add the endpoint to.</param>
    /// <param name="pathMatch">The local path pattern for the BFF API endpoint.</param>
    /// <param name="apiAddress">The remote API address to which requests will be forwarded.</param>
    /// <param name="yarpTransformBuilder">
    /// Optional. An action to configure YARP transforms for this proxy request. 
    /// If not provided, a default transform builder is used.
    /// </param>
    /// <param name="requestConfig">
    /// Optional. Additional configuration for the forwarded request, such as timeouts or activity propagation.
    /// If not specified, the default yarp configuration is used.
    /// </param>
    /// <returns>An <see cref="IEndpointConventionBuilder"/> for further configuration of the endpoint.</returns>
    public static IEndpointConventionBuilder MapRemoteBffApiEndpoint(
        this IEndpointRouteBuilder endpoints,
        PathString pathMatch,
        Uri apiAddress,
        Action<TransformBuilderContext>? yarpTransformBuilder = null,
        ForwarderRequestConfig? requestConfig = null
        )
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentNullException.ThrowIfNull(apiAddress);
        endpoints.CheckLicense();

        // See if a default request config is registered in DI, otherwise use an empty one
        requestConfig ??= endpoints.ServiceProvider.GetService<ForwarderRequestConfig>() ?? ForwarderRequestConfig.Empty;

        // Configure the yarp transform pipeline. Either use the one provided or the default
        yarpTransformBuilder ??= context =>
        {
            // For the default, either get one from DI (to globally configure a default) 
            var defaultYarpTransformBuilder = context.Services.GetService<BffYarpTransformBuilder>()
                // or use the built-in default
                ?? DefaultBffYarpTransformerBuilders.DirectProxyWithAccessToken;

            // invoke the default transform builder
            defaultYarpTransformBuilder(pathMatch, context);
        };

        // Try to resolve the ITransformBuilder from DI. If it is not registered,
        // throw a clearer exception. Otherwise, the call below fails with a less clear exception.
        _ = endpoints.ServiceProvider.GetService<ITransformBuilder>()
            ?? throw new InvalidOperationException("No ITransformBuilder has been registered. Have you called BffBuilder.AddRemoteApis()");

        return endpoints.MapForwarder(
                pattern: pathMatch.Add("/{**catch-all}").Value!,
                destinationPrefix: apiAddress.ToString(),
                configureTransform: context =>
                {
                    yarpTransformBuilder(context);
                },
                requestConfig: requestConfig)
            .WithMetadata(new BffRemoteApiEndpointMetadata());
    }

}
