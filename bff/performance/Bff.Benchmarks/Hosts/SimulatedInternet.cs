// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using Duende.Bff.DynamicFrontends;

namespace Bff.Benchmarks.Hosts;

internal class SimulatedInternet : DelegatingHandler
{
    private readonly RoutingMessageHandler _routingHandler = new();


#if DEBUG
    public bool UseKestrel { get; } = false;
#else
    public bool UseKestrel { get; } = true;
#endif


    public SimulatedInternet()
    {
        if (UseKestrel)
        {
            InnerHandler = new SocketsHttpHandler()
            {
                UseCookies = false,
                AllowAutoRedirect = false
            };
        }
        else
        {
            InnerHandler = _routingHandler;
        }
    }

    public void AddHandler(Host host)
    {
        var url = host.Url();
        AddHandler(url, host.Server.CreateHandler());
    }

    public void AddHandler(Origin origin, HttpMessageHandler handler)
    {
        if (!UseKestrel)
        {
            _routingHandler.AddHandler(origin, handler);
        }
    }

    public void AddHandler(Uri url, HttpMessageHandler handler) => AddHandler(Origin.Parse(url), handler);


    public HttpClient BuildHttpClient(Uri baseUrl)
    {
        var handler = new RedirectHandler();

        handler.InnerHandler = new CookieHandler(this, new CookieContainer());

        var client = new HttpClient(handler);
        client.BaseAddress = baseUrl;
        return client;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CT ct)
    {
        var httpResponseMessage = await base.SendAsync(request, ct);
        return httpResponseMessage;
    }
}
