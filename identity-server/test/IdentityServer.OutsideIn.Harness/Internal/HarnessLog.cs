// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using Xunit;

namespace Duende.IdentityServer.OutsideIn.Harness.Internal;

/// <summary>
/// Timing log for one environment, written to the test's output (design doc D18: setup steps are logged with how long
/// they took, so slow setups show up). Each line shows the time since the environment started.
/// <list type="bullet">
/// <item>Setup steps (servers, nodes, load balancers, client apps, Verify) show how long they took, indented under the
/// step that contains them.</item>
/// <item>Every HTTP request any harness client sends (the browser, client apps' and IdentityServer's back channels) is
/// logged when it starts (<c>=&gt;</c>) and when its response arrives (<c>&lt;=</c>), with a number. A request sent while
/// a host handles another request gets a sub-number of it: while handling request <c>1</c>, a server's first call is
/// <c>1.1</c>. The number travels with the request in the <see cref="RequestHeader" /> header.</item>
/// </list>
/// </summary>
internal sealed class HarnessLog
{
    /// <summary>Carries a request's number to the host that handles it, so its own requests get sub-numbers.</summary>
    public const string RequestHeader = "X-OutsideIn-Request";

    private readonly Stopwatch _sinceStart = Stopwatch.StartNew();
    private readonly ITestOutputHelper? _output = TestContext.Current.TestOutputHelper;
    private readonly Lock _lock = new();
    private readonly AsyncLocal<RequestNumber?> _handling = new();
    private readonly ConcurrentDictionary<string, RequestNumber> _requests = new(StringComparer.Ordinal);
    private int _depth;
    private int _rootRequests;

    /// <summary>Times a step. Steps started inside it are indented under it.</summary>
    public Step Measure(string name)
    {
        int depth;
        lock (_lock)
        {
            depth = _depth++;
        }

        return new Step(this, name, depth);
    }

    /// <summary>Records something that happened, with how long it took.</summary>
    public void Record(string name, TimeSpan duration) => Write($"{name}: {duration.TotalMilliseconds:F0} ms");

    /// <summary>
    /// Numbers a request a harness client is about to send: a sub-number of the request the current host is handling,
    /// or the next top-level number when the test sent it.
    /// </summary>
    public RequestNumber NextRequest()
    {
        var number = _handling.Value is { } parent
            ? parent.NextChild()
            : new RequestNumber(Interlocked.Increment(ref _rootRequests).ToString(System.Globalization.CultureInfo.InvariantCulture));
        _requests[number.Id] = number;
        return number;
    }

    /// <summary>Marks the request with this number as the one the current host is handling, until the scope ends.</summary>
    public void Handling(string? requestId) =>
        _handling.Value = requestId is not null && _requests.TryGetValue(requestId, out var number) ? number : null;

    /// <summary>Records which node a load balancer sent a request to, for that request's end line.</summary>
    public void ServedBy(string? requestId, string node)
    {
        if (requestId is not null && _requests.TryGetValue(requestId, out var number))
        {
            number.ServedBy = node;
        }
    }

    /// <summary>Writes a line about one request, indented by how deep its number is.</summary>
    public void WriteRequest(RequestNumber number, string message) =>
        Write($"{new string(' ', number.Depth * 2)}{number.Id} {message}");

    /// <summary>Writes a line about something that happened while a request was sent, under that request.</summary>
    public void WriteForRequest(string? requestId, string message)
    {
        if (requestId is not null && _requests.TryGetValue(requestId, out var number))
        {
            WriteRequest(number, $"   {message}");
        }
        else
        {
            Write(message);
        }
    }

    public void Write(string message) => Write(depth: 0, message);

    private void Write(int depth, string message)
    {
        var line = $"[outside-in {_sinceStart.Elapsed.TotalMilliseconds,6:F0} ms] {new string(' ', depth * 2)}{message}";
        try
        {
            _output?.WriteLine(line);
        }
        catch (InvalidOperationException)
        {
            // The test has finished (for example while disposing after a failure); there is nowhere left to write.
        }
    }

    internal readonly struct Step(HarnessLog log, string name, int depth) : IDisposable
    {
        private readonly long _started = Stopwatch.GetTimestamp();

        public void Dispose()
        {
            lock (log._lock)
            {
                log._depth--;
            }

            log.Write(depth, $"{name}: {Stopwatch.GetElapsedTime(_started).TotalMilliseconds:F0} ms");
        }
    }
}

/// <summary>A logged request's number, such as <c>1</c> or <c>1.2</c>, and what is known about it.</summary>
internal sealed class RequestNumber(string id)
{
    private int _children;

    public string Id { get; } = id;

    public int Depth => Id.Count(character => character == '.');

    /// <summary>The node a load balancer sent the request to, if it went through one.</summary>
    public string? ServedBy { get; set; }

    public RequestNumber NextChild() => new($"{Id}.{Interlocked.Increment(ref _children)}");
}
