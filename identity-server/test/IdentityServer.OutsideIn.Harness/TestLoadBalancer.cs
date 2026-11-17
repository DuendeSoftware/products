// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using Duende.IdentityServer.OutsideIn.Harness.Internal;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.LoadBalancing;
using Yarp.ReverseProxy.Model;
using Yarp.ReverseProxy.Transforms;

namespace Duende.IdentityServer.OutsideIn.Harness;

/// <summary>One entry in <see cref="TestLoadBalancer.Log" />: which node served which request, and when.</summary>
public sealed record LoadBalancerLogEntry(string Path, string NodeId, DateTimeOffset Timestamp);

/// <summary>
/// A real reverse proxy (YARP) in front of a topology's nodes (design doc D11/D12). Default policy is
/// round-robin. Every request it forwards is recorded in <see cref="Log" /> with the node that served it.
/// </summary>
public sealed class TestLoadBalancer : IAsyncDisposable
{
    private readonly WebApplication _app;
    private readonly ConcurrentQueue<LoadBalancerLogEntry> _log;

    private TestLoadBalancer(WebApplication app, int port, ConcurrentQueue<LoadBalancerLogEntry> log)
    {
        _app = app;
        Port = port;
        _log = log;
    }

    /// <summary>The loopback port Kestrel bound to.</summary>
    public int Port { get; }

    /// <summary>Which node served each request so far, oldest first.</summary>
    public IReadOnlyCollection<LoadBalancerLogEntry> Log => _log.ToArray();

    internal static async Task<TestLoadBalancer> StartAsync(
        string topologyName,
        IReadOnlyList<IdentityServerNode> nodes,
        TestClock clock,
        HarnessLog harnessLog,
        Ct cancellationToken)
    {
        var log = new ConcurrentQueue<LoadBalancerLogEntry>();

        using (harnessLog.Measure("harness TLS certificate (created once per test process)"))
        {
            _ = TestCertificate.Instance;
        }

        var builder = HarnessHosting.CreateBuilder(clock);
        builder.ListenOnLoopback(https: true);

        var clusterId = $"{topologyName}-cluster";
        var routeConfig = new RouteConfig
        {
            RouteId = $"{topologyName}-route",
            ClusterId = clusterId,
            Match = new RouteMatch { Path = "/{**catch-all}" },
            // Preserves the inbound Host header (the harness's virtual host, e.g. "sut") instead of YARP
            // replacing it with the destination node's loopback address. Without this, IdentityServer would
            // compute its issuer and endpoint URLs from "127.0.0.1:<node-port>" instead of the virtual host.
            Transforms = [new Dictionary<string, string> { ["RequestHeaderOriginalHost"] = "true" }],
        };

        var clusterConfig = new ClusterConfig
        {
            ClusterId = clusterId,
            LoadBalancingPolicy = LoadBalancingPolicies.RoundRobin,
            Destinations = nodes.ToDictionary(
                node => node.Id,
                node => new DestinationConfig { Address = $"http://127.0.0.1:{node.Port}" }),
        };

        builder.Services
            .AddReverseProxy()
            .LoadFromMemory([routeConfig], [clusterConfig])
            .ConfigureHttpClient((_, handler) => handler.ConnectCallback = async (context, cancellationToken) =>
            {
                // The destinations are loopback addresses, so this needs no DNS. Timed, to show connection costs.
                var started = Stopwatch.GetTimestamp();
                var stream = await VirtualHostRegistry.ConnectToLoopbackAsync(context.DnsEndPoint.Port, cancellationToken);
                harnessLog.WriteForRequest(
                    VirtualHostRegistry.RequestId(context.InitialRequestMessage),
                    $"new connection: {topologyName} load balancer -> node port {context.DnsEndPoint.Port}, socket connect {Stopwatch.GetElapsedTime(started).TotalMilliseconds:F0} ms");
                return stream;
            })
            .AddTransforms(transformBuilder => transformBuilder.AddRequestTransform(transformContext =>
            {
                var destinationId = transformContext.HttpContext.Features.Get<IReverseProxyFeature>()?.ProxiedDestination?.DestinationId;
                if (destinationId is not null)
                {
                    harnessLog.ServedBy(transformContext.HttpContext.Request.Headers[HarnessLog.RequestHeader].FirstOrDefault(), destinationId);
                    log.Enqueue(new LoadBalancerLogEntry(
                        transformContext.HttpContext.Request.Path.Value ?? "/",
                        destinationId,
                        clock.TimeProvider.GetUtcNow()));
                }

                return default;
            }));

        var app = builder.Build();
        app.MapReverseProxy();

        await app.StartAsync(cancellationToken);

        return new TestLoadBalancer(app, app.GetPort(), log);
    }

    public async ValueTask DisposeAsync() => await _app.StopAsync();

    // TODO (design doc D12): per-request routing. RouteTo(node) should cover requests a test sends inside a
    // scope; Route(match, to) should cover any request, including a client app's back-channel calls. Neither
    // is needed by the discovery-endpoint scenario this slice ships with, so both are left unimplemented.
    public IDisposable RouteTo(IdentityServerNode node) =>
        throw new NotSupportedException("TestLoadBalancer.RouteTo is not implemented in this first slice of the harness.");

    public void Route(Func<HttpRequestLogEntry, bool> match, IdentityServerNode to) =>
        throw new NotSupportedException("TestLoadBalancer.Route is not implemented in this first slice of the harness.");
}

/// <summary>Draft shape for the request a <see cref="TestLoadBalancer.Route" /> rule matches against. Not implemented.</summary>
public sealed record HttpRequestLogEntry(string Path);
