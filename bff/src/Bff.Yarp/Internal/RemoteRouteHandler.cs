// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.Configuration;
using Duende.Bff.DynamicFrontends;
using Microsoft.AspNetCore.Http;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Forwarder;
using Yarp.ReverseProxy.Transforms.Builder;

namespace Duende.Bff.Yarp.Internal;
internal class RemoteRouteHandler(
    SelectedFrontend selectedFrontend,
    IHttpForwarder httpForwarder,
    ITransformBuilder transformBuilder,
    IForwarderHttpClientFactory? forwarderHttpClientFactory = null,
    BffYarpTransformBuilder? customBffYarpTransformBuilder = null
    )
{
    private IForwarderHttpClientFactory _forwarderHttpClientFactory = forwarderHttpClientFactory ?? new ForwarderHttpClientFactory();

    public async Task<bool> HandleAsync(HttpContext context, CancellationToken ct)
    {

        if (!selectedFrontend.TryGet(out var frontend))
        {
            return false;
        }

        var invoker = _forwarderHttpClientFactory.CreateClient(new ForwarderHttpClientContext()
        {
            NewConfig = new HttpClientConfig()
            {

            }
        });
        var requestConfig = new ForwarderRequestConfig
        {
            // todo: timeout configurable?
            ActivityTimeout = TimeSpan.FromSeconds(100)
        };

        var bffTransformBuilder = customBffYarpTransformBuilder ??
             DefaultBffYarpTransformerBuilders.DirectProxyWithAccessToken;

        foreach (var route in frontend.GetRemoteApis())
        {

            if (context.Request.Path.StartsWithSegments(route.LocalPath.ToString()))
            {
                var bffRemoteApiEndpointMetadata = new BffRemoteApiEndpointMetadata()
                {
                    // Todo: EV: Should we allow somehow the TokenParameters to be set?
                    TokenType = route.RequiredTokenType
                };
                context.SetEndpoint(new Endpoint(null, new EndpointMetadataCollection(bffRemoteApiEndpointMetadata), null));

                // Todo: EV: Should we cache this?
                var httpTransformer = transformBuilder.Create(c => bffTransformBuilder(route.LocalPath, c));
                var destinationPrefix = route.TargetUri.ToString();

                var error = await httpForwarder.SendAsync(context, destinationPrefix, invoker, requestConfig,
                    httpTransformer, ct);

                // Todo: EV: what to do with an error here. 

                return true;
            }
        }
        // No routes matched
        return false;
    }
}
