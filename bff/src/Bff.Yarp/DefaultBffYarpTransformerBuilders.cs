// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Builder;
using Yarp.ReverseProxy.Transforms;
using Yarp.ReverseProxy.Transforms.Builder;

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
    public static BffYarpTransformBuilder DirectProxyWithAccessToken =
        (localPath, context) =>
        {
            context.AddRequestHeaderRemove("Cookie");
            context.AddPathRemovePrefix(localPath);
            context.AddBffAccessToken(localPath);
        };
}

/// <summary>
/// Delegate for pipeline transformers. 
/// </summary>
/// <param name="localPath">The local path that should be proxied. This path will be removed from the proxied request. </param>
/// <param name="context">The transform builder context</param>
public delegate void BffYarpTransformBuilder(string localPath, TransformBuilderContext context);
