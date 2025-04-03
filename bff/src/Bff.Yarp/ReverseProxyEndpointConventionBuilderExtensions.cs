// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Duende.Bff.Yarp;

/// <summary>
/// Extensions methods to wire up BFF-enhanced YARP
/// </summary>
public static class ReverseProxyEndpointConventionBuilderExtensions
{
    /// <summary>
    /// Adds YARP with anti-forgery protection
    /// </summary>
    /// <param name="endpoints"></param>
    /// <param name="configureAction"></param>
    /// <returns></returns>
    public static ReverseProxyConventionBuilder MapBffReverseProxy(this IEndpointRouteBuilder endpoints,
        Action<IReverseProxyApplicationBuilder> configureAction)
    {
        return endpoints.MapReverseProxy(configureAction)
            .AsBffApiEndpoint();
    }

    /// <summary>
    /// Adds YARP with anti-forgery protection 
    /// </summary>
    /// <param name="endpoints"></param>
    /// <returns></returns>
    public static ReverseProxyConventionBuilder MapBffReverseProxy(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapReverseProxy()
            .AsBffApiEndpoint();
    }

    // TODO: do we also need a SkipAntiforgery API?
    // TODO: review the API comment below

    /// <summary>
    /// Adds anti-forgery protection to YARP
    /// </summary>
    /// <param name="builder"></param>
    /// <returns></returns>
    public static ReverseProxyConventionBuilder AsBffApiEndpoint(this ReverseProxyConventionBuilder builder)
    {
        return builder.WithMetadata(new BffApiAttribute());
    }
}
