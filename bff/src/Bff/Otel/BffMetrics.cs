// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Diagnostics.Metrics;

namespace Duende.Bff.Otel;

public sealed class BffMetrics : IDisposable
{
    public const string MeterName = "Duende.Bff";

    private readonly Counter<int> _sessionStarted;
    private readonly Counter<int> _sessionEnded;
    private Meter _meter;

    public BffMetrics(IMeterFactory meterFactory)
    {
        _meter = meterFactory.Create(MeterName);

        _sessionStarted = _meter.CreateCounter<int>("session.started", "count", "Number of sessions started");
        _sessionEnded = _meter.CreateCounter<int>("session.ended", "count", "Number of sessions ended");
    }

    public void SessionStarted() => _sessionStarted.Add(1);

    public void SessionEnded() => _sessionEnded.Add(1);

    public void SessionsEnded(int count) => _sessionEnded.Add(count);

    public void Dispose() => _meter.Dispose();
}
