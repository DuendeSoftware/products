// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Diagnostics.Metrics;

namespace Duende.Bff.Otel;

public sealed class BffMetrics
{
    public const string MeterName = "Duende.Bff";

    private readonly Counter<int> _sessionStarted;
    private readonly Counter<int> _sessionEnded;

    public BffMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);

        _sessionStarted = meter.CreateCounter<int>("session.started", "count", "Number of sessions started");
        _sessionEnded = meter.CreateCounter<int>("session.ended", "count", "Number of sessions ended");
    }

    public void SessionStarted() => _sessionStarted.Add(1);

    public void SessionEnded() => _sessionEnded.Add(1);

    public void SessionsEnded(int count) => _sessionEnded.Add(count);
}
