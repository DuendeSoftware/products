// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Diagnostics.Metrics;

namespace Duende.Spaces.Internal.Telemetry;

internal sealed class SpacesMetrics
{
    private static readonly Version? AssemblyVersion = typeof(SpacesMetrics).Assembly.GetName().Version;

    private static readonly string ServiceVersion = AssemblyVersion is { } v
        ? $"{v.Major}.{v.Minor}.{v.Build}"
        : "0.0.0";

    private readonly Counter<long> _defaultSpaceFallback;

    public SpacesMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(new MeterOptions(SpacesTelemetryConstants.MeterName) { Version = ServiceVersion });
        _defaultSpaceFallback = meter.CreateCounter<long>(
            SpacesTelemetryConstants.Instruments.DataProtectionDefaultSpaceFallback,
            description: "Number of values unprotected in the Default space through the Default-space fallback.");
    }

    public void RecordDefaultSpaceFallback(string operation) =>
        _defaultSpaceFallback.Add(1, new KeyValuePair<string, object?>(SpacesTelemetryConstants.Tags.DataProtectionOperation, operation));
}
