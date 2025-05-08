// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using System.Diagnostics.Metrics;

namespace IdentityServer.UnitTests.Telemetry;

public class TelemetryTests
{
    [Fact]
    public void ServiceName_is_Duende_IdentityServer()
    {
        Duende.IdentityServer.Telemetry.ServiceName
            .ShouldBe("Duende.IdentityServer");
    }

    [Fact]
    public void ServiceVersion_is_not_null()
    {
        Duende.IdentityServer.Telemetry.ServiceVersion
            .ShouldNotBeNull();
    }

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

        results.Count.ShouldBe(1);
        results[0].Name.ShouldBe("tokenservice.operation");
        results[0].Value.ShouldBe(1);
    }

    [Fact]
    public void Success_adds_to_operation_counter()
    {
        using var measurements = StartListeningForMeasurements(out var results);

        Duende.IdentityServer.Telemetry.Metrics.Success("test.client");

        results.Count.ShouldBe(1);
        results[0].Name.ShouldBe("tokenservice.operation");
        results[0].Value.ShouldBe(1);
        results[0].Tags[0].Value.ShouldBe("test.client");
    }

    private static MeterListener StartListeningForMeasurements(
        out List<(string Name, long Value, KeyValuePair<string, object?>[] Tags)> results)
    {
        var listener = new MeterListener();
        List<(string Name, long Value, KeyValuePair<string, object?>[] Tags)> measurements = new();

        listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, _) =>
        {
            measurements.Add((instrument.Name, measurement, tags.ToArray()));
        });

        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == Duende.IdentityServer.Telemetry.ServiceName)
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.Start();
        results = measurements;
        return listener;
    }
}
