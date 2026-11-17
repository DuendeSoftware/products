// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.Builder;

namespace Duende.IdentityServer.OutsideIn.Harness.Internal;

/// <summary>
/// Maps the harness's virtual host names (for example "sut") to the loopback port that serves them, and hands out the
/// handler every HttpClient in the environment uses: the TestBrowser, the client apps' back channel and IdentityServer's
/// calls to external IdPs. Connections resolve through this map instead of DNS (design doc section 09: "Host names
/// without DNS"). Every request is logged when it starts and ends (see <see cref="HarnessLog" />), with the system it
/// targets, and every new connection with how long the socket connect and TLS handshake took.
/// </summary>
internal sealed class VirtualHostRegistry(HarnessLog log)
{
    private static readonly HttpRequestOptionsKey<long> ConnectStarted = new("OutsideIn.ConnectStarted");

    private readonly ConcurrentDictionary<string, (int Port, string System)> _hosts = new(StringComparer.OrdinalIgnoreCase);

    /// <param name="host">The virtual host name, for example <c>sut</c>.</param>
    /// <param name="port">The loopback port that serves it.</param>
    /// <param name="system">What runs there, for the request log, for example <c>IdentityServer sut</c> or <c>client app web</c>.</param>
    public void Register(string host, int port, string system) => _hosts[host] = (port, system);

    public string DescribeSystem(string host) => _hosts.TryGetValue(host, out var entry) ? entry.System : host;

    /// <param name="clientName">Who sends the requests, for the request log, for example <c>browser</c> or <c>web back channel</c>.</param>
    /// <param name="configure">Changes to the connection handler, for example to switch off automatic redirects.</param>
    public HttpMessageHandler CreateHandler(string clientName, Action<SocketsHttpHandler>? configure = null)
    {
        var sockets = new SocketsHttpHandler
        {
            ConnectCallback = ConnectAsync,
            PlaintextStreamFilter = (context, _) =>
            {
                // Runs once the connection is ready, after the TLS handshake on HTTPS.
                if (context.InitialRequestMessage.RequestUri?.Scheme == Uri.UriSchemeHttps
                    && context.InitialRequestMessage.Options.TryGetValue(ConnectStarted, out var started))
                {
                    log.WriteForRequest(RequestId(context.InitialRequestMessage), $"new connection: connect + TLS {Stopwatch.GetElapsedTime(started).TotalMilliseconds:F0} ms");
                }

                return ValueTask.FromResult(context.PlaintextStream);
            },
            SslOptions =
            {
                // Accept the harness's own certificate, and nothing else.
                RemoteCertificateValidationCallback = (_, certificate, _, _) => TestCertificate.IsHarnessCertificate(certificate),
            },
        };
        configure?.Invoke(sockets);
        return new RequestLoggingHandler(this, log, clientName, sockets);
    }

    internal static string? RequestId(HttpRequestMessage request) =>
        request.Headers.TryGetValues(HarnessLog.RequestHeader, out var values) ? values.FirstOrDefault() : null;

    private async ValueTask<Stream> ConnectAsync(SocketsHttpConnectionContext context, Ct cancellationToken)
    {
        var host = context.DnsEndPoint.Host;
        if (!_hosts.TryGetValue(host, out var entry))
        {
            throw new InvalidOperationException(
                $"The outside-in test harness has no server, load balancer or client app registered for host '{host}'. " +
                "Every host a test connects to must come from the environment (for example IIssuer.Url or IWebApp.BaseUrl).");
        }

        var started = Stopwatch.GetTimestamp();
        context.InitialRequestMessage.Options.Set(ConnectStarted, started);
        var stream = await ConnectToLoopbackAsync(entry.Port, cancellationToken);
        log.WriteForRequest(RequestId(context.InitialRequestMessage), $"new connection: socket connect {Stopwatch.GetElapsedTime(started).TotalMilliseconds:F0} ms");
        return stream;
    }

    /// <summary>Opens a TCP connection to a loopback port, without DNS.</summary>
    public static async ValueTask<Stream> ConnectToLoopbackAsync(int port, Ct cancellationToken)
    {
        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        try
        {
            await socket.ConnectAsync(new IPEndPoint(IPAddress.Loopback, port), cancellationToken);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    /// <summary>Numbers every request, sends the number along, and logs its start and end.</summary>
    private sealed class RequestLoggingHandler(VirtualHostRegistry registry, HarnessLog log, string clientName, HttpMessageHandler inner)
        : DelegatingHandler(inner)
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, Ct cancellationToken)
        {
            var number = log.NextRequest();
            request.Headers.Remove(HarnessLog.RequestHeader);
            request.Headers.Add(HarnessLog.RequestHeader, number.Id);

            var uri = request.RequestUri!;
            var target = $"{request.Method} {uri.GetLeftPart(UriPartial.Path)}";
            log.WriteRequest(number, $"=> {target}   ({clientName} -> {registry.DescribeSystem(uri.Host)})");

            var started = Stopwatch.GetTimestamp();
            try
            {
                var response = await base.SendAsync(request, cancellationToken);
                var servedBy = number.ServedBy is null ? "" : $", served by {number.ServedBy}";
                log.WriteRequest(number, $"<= {target}: {(int)response.StatusCode} {response.ReasonPhrase}, {Stopwatch.GetElapsedTime(started).TotalMilliseconds:F0} ms{servedBy}");
                return response;
            }
            catch (Exception exception)
            {
                log.WriteRequest(number, $"<= {target}: failed after {Stopwatch.GetElapsedTime(started).TotalMilliseconds:F0} ms with {exception.GetType().Name}: {exception.Message}");
                throw;
            }
        }
    }
}

/// <summary>Lets a host's own requests get sub-numbers of the request it's handling (see <see cref="HarnessLog" />).</summary>
internal static class RequestNumberingExtensions
{
    public static void UseRequestNumbering(this WebApplication app, HarnessLog log) =>
        app.Use(async (context, next) =>
        {
            // AsyncLocal: the value flows into everything this request awaits, and is gone when the middleware returns.
            log.Handling(context.Request.Headers[HarnessLog.RequestHeader].FirstOrDefault());
            await next(context);
        });
}
