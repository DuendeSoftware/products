// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Diagnostics;
using System.Net;
using Duende.Bff.DynamicFrontends;

namespace Duende.Bff.Tests.TestInfra;

public class SimulatedInternet : DelegatingHandler
{
    private readonly RoutingMessageHandler _routingHandler = new();
    private readonly WriteTestOutput _outputWriter;

    public SimulatedInternet(WriteTestOutput outputWriter)
    {
        _outputWriter = outputWriter;
        InnerHandler = _routingHandler;
    }

    public void AddHandler(TestHost host)
    {
        var url = host.Url();
        AddHandler(url, host.Server.CreateHandler());
    }

    public void AddHandler(Origin origin, HttpMessageHandler handler) => _routingHandler.AddHandler(origin, handler);

    public void AddHandler(Uri url, HttpMessageHandler handler) => AddHandler(Origin.Parse(url), handler);

    public void AddCustomHandler(Uri map, TestHost to) => AddHandler(Origin.Parse(map), to.Server.CreateHandler());


    public T BuildHttpClient<T>(Uri baseUrl) where T : HttpClient, IHttpClient<T>
    {
        var handler = new RedirectHandler(_outputWriter);
        var cookieContainer = new CookieContainer();
        handler.InnerHandler = new CookieHandler(this, cookieContainer);

        var client = T.Build(handler, cookieContainer);
        client.BaseAddress = baseUrl;
        return client;

    }

    public HttpClient BuildHttpClient(Uri baseUrl)
    {
        var handler = new RedirectHandler(_outputWriter);
        handler.InnerHandler = new CookieHandler(this, new CookieContainer());

        var client = new HttpClient(handler);
        client.BaseAddress = baseUrl;
        return client;
    }

    public void Clear() => _routingHandler.Clear();

    private int _requestIdSeed = 0;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CT ct)
    {
        var requestId = Interlocked.Increment(ref _requestIdSeed);

        try
        {
            var stopwatch = Stopwatch.StartNew();
            _outputWriter($"Started Request {requestId} to {request.RequestUri}");
            var httpResponseMessage = await base.SendAsync(request, ct);

            _outputWriter(
                $"Completed Request {requestId} to {request.RequestUri} took {stopwatch.ElapsedMilliseconds}ms and returned {httpResponseMessage.StatusCode}");
            return httpResponseMessage;
        }
        catch (Exception ex)
        {
            _outputWriter($"Exception while sending request {requestId} . " + ex);
            throw;
        }
    }
}
