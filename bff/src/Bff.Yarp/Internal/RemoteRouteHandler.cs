// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Collections.Concurrent;
using Duende.Bff.Configuration;
using Duende.Bff.DynamicFrontends;
using Microsoft.AspNetCore.Http;
using Yarp.ReverseProxy.Forwarder;
using Yarp.ReverseProxy.Transforms.Builder;

namespace Duende.Bff.Yarp.Internal;
internal class RemoteRouteHandler(
    CurrentFrontendAccessor currentFrontendAccessor,
    IHttpForwarder httpForwarder,
    ITransformBuilder transformBuilder,
    IForwarderHttpClientFactory? forwarderHttpClientFactory = null,
    BffYarpTransformBuilder? customBffYarpTransformBuilder = null
    )
{
    private readonly IForwarderHttpClientFactory _forwarderHttpClientFactory = forwarderHttpClientFactory ?? new ForwarderHttpClientFactory();
    private readonly ConcurrentDictionary<LocalPath, HttpTransformer> _cachedTransformersPerPath = new();

    public async Task<bool> HandleAsync(HttpContext context, CancellationToken ct)
    {

        if (!currentFrontendAccessor.TryGet(out var frontend))
        {
            return false;
        }

        // a HTTP invoker is like a http client
        // since we get it from a factory, it should be disposed after use
        using var invoker = _forwarderHttpClientFactory.CreateClient(new ForwarderHttpClientContext());

        var bffTransformBuilder = customBffYarpTransformBuilder ??
             DefaultBffYarpTransformerBuilders.DirectProxyWithAccessToken;

        foreach (var route in frontend.GetRemoteApis())
        {
            var requestConfig = new ForwarderRequestConfig
            {
                ActivityTimeout = route.ActivityTimeout,
                AllowResponseBuffering = route.AllowResponseBuffering,
            };

            // Path matching must be case insensitive
            if (context.Request.Path.StartsWithSegments(route.LocalPath.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                var bffRemoteApiEndpointMetadata = new BffRemoteApiEndpointMetadata()
                {
                    TokenType = route.RequiredTokenType,
                    BffUserAccessTokenParameters = route.Parameters,
                };

                if (route.AccessTokenRetrieverType != null)
                {
                    bffRemoteApiEndpointMetadata.AccessTokenRetriever = route.AccessTokenRetrieverType;
                }

                context.SetEndpoint(new Endpoint(null, new EndpointMetadataCollection(bffRemoteApiEndpointMetadata), null));

                var httpTransformer = _cachedTransformersPerPath.GetOrAdd(
                    key: route.LocalPath,
                    valueFactory: (p) => transformBuilder.Create(c => bffTransformBuilder(p, c)));

                var destinationPrefix = route.TargetUri.ToString();

                await httpForwarder.SendAsync(context, destinationPrefix, invoker, requestConfig,
                    httpTransformer, ct);

                return true;
            }
        }
        // No routes matched
        return false;
    }
}
