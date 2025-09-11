// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.Configuration;
using Yarp.ReverseProxy.Transforms;

namespace Duende.Bff.Yarp;

/// <summary>
/// Contains the default transformer logic for YARP BFF endpoints. 
/// </summary>
public static class DefaultBffYarpTransformerBuilders
{
    /// <summary>
    /// Build a default 'direct proxy' transformer. This removes the 'cookie' header, removes the local path prefix,
    /// and adds an access token to the request. The type of access token is determined by the <see cref="BffRemoteApiEndpointMetadata"/>.
    /// </summary>
    public static readonly BffYarpTransformBuilder DirectProxyWithAccessToken =
        (pathMatch, context) =>
        {
            context.AddRequestHeaderRemove("Cookie");
            context.AddPathRemovePrefix(pathMatch);
            context.AddBffAccessToken(pathMatch);
        };
}
