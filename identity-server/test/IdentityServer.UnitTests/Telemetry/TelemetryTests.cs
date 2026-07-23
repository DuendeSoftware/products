// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using System.Collections.Concurrent;
using System.Diagnostics.Metrics;

namespace IdentityServer.UnitTests.Telemetry;

public class TelemetryTests
{
    private const string OperationCounterName = "tokenservice.operation";

    [Fact]
    public void ServiceName_is_Duende_IdentityServer() => Duende.IdentityServer.Telemetry.ServiceName.ShouldBe("Duende.IdentityServer");

    [Fact]
    public void ServiceVersion_is_not_null() => Duende.IdentityServer.Telemetry.ServiceVersion.ShouldNotBeNull();

    [Fact]
    public void Meter_should_has_service_name_of_Duende_IdentityServer()
    {
        var meter = Duende.IdentityServer.Telemetry.Metrics.Meter;
        meter.Name.ShouldBe("Duende.IdentityServer");
    }

    [Fact]
    public void Failure_adds_to_operation_counter()
    {
        using var measurements = StartListeningForMeasurements(out var results);

        Duende.IdentityServer.Telemetry.Metrics.Failure("oops");

        var result = results.First(x => x.Name == OperationCounterName
            && x.Tags.Any(t => t.Key == Duende.IdentityServer.Telemetry.Metrics.Tags.Error && t.Value?.ToString() == "oops"));
        result.Value.ShouldBe(1);
    }

    [Fact]
    public void Success_adds_to_operation_counter()
    {
        using var measurements = StartListeningForMeasurements(out var results);

        Duende.IdentityServer.Telemetry.Metrics.Success("test.client");

        results.ShouldNotBeEmpty();

        var result = results.First(x => x.Name == OperationCounterName
            && x.Tags.Any(t => t.Key == Duende.IdentityServer.Telemetry.Metrics.Tags.Client && t.Value?.ToString() == "test.client"));

        result.Tags.First(t => t.Key == Duende.IdentityServer.Telemetry.Metrics.Tags.Client).Value.ShouldBe("test.client");
        result.Value.ShouldBeGreaterThan(0, "sometimes the test runs in parallel and reports multiple measurements");
    }

    private static MeterListener StartListeningForMeasurements(
        out ConcurrentBag<(string Name, long Value, KeyValuePair<string, object?>[] Tags)> results)
    {
        var listener = new MeterListener();
        var measurements = new ConcurrentBag<(string Name, long Value, KeyValuePair<string, object?>[] Tags)>();

        listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, _) =>
        {
            var entry = (instrument.Name, measurement, tags.ToArray());
            measurements.Add(entry);
        });

        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == Duende.IdentityServer.Telemetry.ServiceName &&
                instrument.Name == OperationCounterName)
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.Start();
        results = measurements;
        return listener;
    }
}
