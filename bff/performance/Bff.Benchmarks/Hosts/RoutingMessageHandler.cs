// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.DynamicFrontends;

namespace Bff.Benchmarks.Hosts;

/// <summary>
///     An <see cref="HttpMessageHandler"/> that acts like a router
///     between multiple handlers that represent different hosts.
/// </summary>
internal class RoutingMessageHandler : HttpMessageHandler
{
    private readonly Dictionary<string, HostHandler> _hosts = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    ///     Adds a handler for a given HostHeader.
    /// </summary>
    /// <param name="hostHeaderValue">The hostHeader to whom requests are routed to.</param>
    /// <param name="handler">The handler for requests to the specified hostHeader.</param>
    public void AddHandler(HostHeaderValue hostHeaderValue, HttpMessageHandler handler)
    {
        var endpoint = new HostHandler(handler);
        var host = $"{hostHeaderValue.Host}:{hostHeaderValue.Port}";
        _hosts.TryAdd(host, endpoint);
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CT ct)
    {
        var host = $"{request.RequestUri?.Host}:{request.RequestUri?.Port}";

        if (!_hosts.TryGetValue(host, out var hostHandler))
        {
            var hosts = string.Join(", ", _hosts.Keys.Select(k => $"'{k}'"));
            throw new InvalidOperationException($"Host '{host}' not found. Valid hosts are {hosts}");
        }

        return hostHandler.SuppressedSend(request, ct);
    }

    public void Clear() => _hosts.Clear();

    private class HostHandler(HttpMessageHandler innerHandler) : DelegatingHandler(innerHandler)
    {
        internal Task<HttpResponseMessage> SuppressedSend(
            HttpRequestMessage request,
            CT ct)
        {
            Task<HttpResponseMessage> t;
            if (ExecutionContext.IsFlowSuppressed())
            {
                t = Task.Run(() => SendAsync(request, ct), ct);
            }
            else
            {
                // We don't want the executing context to flow to the host handler
                using (ExecutionContext.SuppressFlow())
                {
                    t = Task.Run(() => SendAsync(request, ct), ct);
                }
            }

            return t;
        }
    }
}
