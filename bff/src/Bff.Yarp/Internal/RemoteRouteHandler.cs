// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Collections.Concurrent;
using Duende.Bff.Configuration;
using Duende.Bff.DynamicFrontends;
using Duende.Bff.DynamicFrontends.Internal;
using Microsoft.AspNetCore.Http;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Forwarder;
using Yarp.ReverseProxy.Transforms.Builder;

namespace Duende.Bff.Yarp.Internal;
internal class RemoteRouteHandler : IDisposable
{

    // A cache of transformers for each frontend and local path.
    private readonly ConcurrentDictionary<BffFrontendName, ConcurrentDictionary<LocalPath, HttpTransformer>> _cache = new();

    // In Yarp, the forwarder is created when you call 'map'. 
    private readonly HttpMessageInvoker _invoker;

    private BffYarpTransformBuilder _bffTransformBuilder;

    private readonly CurrentFrontendAccessor _currentFrontendAccessor;
    private readonly IHttpForwarder _httpForwarder;
    private readonly ITransformBuilder _transformBuilder;

    public RemoteRouteHandler(CurrentFrontendAccessor currentFrontendAccessor,
        IHttpForwarder httpForwarder,
        ITransformBuilder transformBuilder,
        FrontendCollection frontendCollection,
        IForwarderHttpClientFactory? forwarderHttpClientFactory = null,
        BffYarpTransformBuilder? customBffYarpTransformBuilder = null)
    {
        _currentFrontendAccessor = currentFrontendAccessor;
        forwarderHttpClientFactory ??= new ForwarderHttpClientFactory();
        _httpForwarder = httpForwarder;
        _transformBuilder = transformBuilder;

        // Create a single invoker that lives until the end of this class.
        // This is similar to what yarp does. https://github.com/dotnet/yarp/blob/main/src/ReverseProxy/Routing/DirectForwardingIEndpointRouteBuilderExtensions.cs#L84
        _invoker = forwarderHttpClientFactory
            .CreateClient(new ForwarderHttpClientContext()
            {
                NewConfig = HttpClientConfig.Empty
            });

        _bffTransformBuilder = customBffYarpTransformBuilder ??
                                    DefaultBffYarpTransformerBuilders.DirectProxyWithAccessToken;

        // When the frontend collection changes, we clear the transformer cache for that frontend.
        // This ensures that any changes to the frontend configuration are reflected in the transformers.
        frontendCollection.OnFrontendChanged += ClearTransformerCacheFor;
    }

    private HttpTransformer GetOrCreateTransformerFor(BffFrontend frontend, RemoteApi api)
    {
        // Yarp creates a transformer per path while mapping the routes. 
        // we also want to do that, but clear the cache when the frontend configuration changes. 
        var transformersForFrontend = _cache
            .GetOrAdd(frontend.Name, _ => new());

        return transformersForFrontend
            .GetOrAdd(api.LocalPath, path => _transformBuilder.Create(c => _bffTransformBuilder(path, c)));
    }

    public void ClearTransformerCacheFor(BffFrontend frontend) => _cache.TryRemove(frontend.Name, out _);

    public async Task<bool> HandleAsync(HttpContext context, CancellationToken ct)
    {
        if (!_currentFrontendAccessor.TryGet(out var frontend))
        {
            return false;
        }

        foreach (var remoteApi in frontend.GetRemoteApis())
        {
            var requestConfig = new ForwarderRequestConfig
            {
                ActivityTimeout = remoteApi.ActivityTimeout,
                AllowResponseBuffering = remoteApi.AllowResponseBuffering,
            };

            // Path matching must be case insensitive
            if (context.Request.Path.StartsWithSegments(remoteApi.LocalPath.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                var bffRemoteApiEndpointMetadata = new BffRemoteApiEndpointMetadata()
                {
                    TokenType = remoteApi.RequiredTokenType,
                    BffUserAccessTokenParameters = remoteApi.Parameters,
                };

                if (remoteApi.AccessTokenRetrieverType != null)
                {
                    bffRemoteApiEndpointMetadata.AccessTokenRetriever = remoteApi.AccessTokenRetrieverType;
                }

                context.SetEndpoint(new Endpoint(null, new EndpointMetadataCollection(bffRemoteApiEndpointMetadata), null));

                var httpTransformer = GetOrCreateTransformerFor(frontend, remoteApi);

                var destinationPrefix = remoteApi.TargetUri.ToString();

                await _httpForwarder.SendAsync(context, destinationPrefix, _invoker, requestConfig,
                    httpTransformer, ct);

                return true;
            }
        }
        // No routes matched
        return false;
    }

    public void Dispose() => _invoker.Dispose();
}
