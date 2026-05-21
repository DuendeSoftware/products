// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Diagnostics.Metrics;
using Duende.Storage.Internal.Telemetry;

namespace Duende.Storage.Sqlite;

public class StorageMetricsTests : IDisposable
{
    private readonly StorageMetrics _metrics = new();
    private readonly MeterListener _listener;
    private readonly List<(string Name, object? Value, KeyValuePair<string, object?>[] Tags)> _recordedMetrics = [];

    public StorageMetricsTests()
    {
        _listener = new MeterListener();
        _listener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == StorageTelemetryConstants.MeterName)
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };
        _listener.SetMeasurementEventCallback<long>(OnMeasurement);
        _listener.SetMeasurementEventCallback<double>(OnMeasurementDouble);
        _listener.Start();
    }

    private void OnMeasurement(Instrument instrument, long measurement, ReadOnlySpan<KeyValuePair<string, object?>> tags, object? state) =>
        _recordedMetrics.Add((instrument.Name, measurement, tags.ToArray()));

    private void OnMeasurementDouble(Instrument instrument, double measurement, ReadOnlySpan<KeyValuePair<string, object?>> tags, object? state) =>
        _recordedMetrics.Add((instrument.Name, measurement, tags.ToArray()));

    public void Dispose()
    {
        _listener.Dispose();
        _metrics.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void meter_name_should_be_correct() =>
        StorageTelemetryConstants.MeterName.ShouldBe("Duende.Storage");

    [Fact]
    public void instrument_names_should_match_spec()
    {
        StorageTelemetryConstants.Instruments.OperationCount.ShouldBe("duende.storage.operation.count");
        StorageTelemetryConstants.Instruments.OperationDuration.ShouldBe("duende.storage.operation.duration");
    }

    [Fact]
    public void tag_names_should_match_spec()
    {
        StorageTelemetryConstants.Tags.Operation.ShouldBe("duende.storage.operation");
        StorageTelemetryConstants.Tags.DbSystem.ShouldBe("db.system");
        StorageTelemetryConstants.Tags.EntityType.ShouldBe("duende.storage.entity_type");
        StorageTelemetryConstants.Tags.Result.ShouldBe("duende.storage.result");
        StorageTelemetryConstants.Tags.ErrorType.ShouldBe("error.type");
    }

    [Fact]
    public void record_success_should_increment_counter_with_correct_tags()
    {
        _metrics.RecordSuccess("create", "mssql", "client");

        _recordedMetrics.ShouldContain(m =>
            m.Name == StorageTelemetryConstants.Instruments.OperationCount &&
            (long)m.Value! == 1 &&
            m.Tags.Any(t => t.Key == StorageTelemetryConstants.Tags.Operation && (string?)t.Value == "create") &&
            m.Tags.Any(t => t.Key == StorageTelemetryConstants.Tags.DbSystem && (string?)t.Value == "mssql") &&
            m.Tags.Any(t => t.Key == StorageTelemetryConstants.Tags.EntityType && (string?)t.Value == "client") &&
            m.Tags.Any(t => t.Key == StorageTelemetryConstants.Tags.Result && (string?)t.Value == "success"));
    }

    [Fact]
    public void record_success_without_entity_type_should_omit_entity_type_tag()
    {
        _metrics.RecordSuccess("read", "postgresql", null);

        _recordedMetrics.ShouldContain(m =>
            m.Name == StorageTelemetryConstants.Instruments.OperationCount &&
            (long)m.Value! == 1 &&
            m.Tags.Any(t => t.Key == StorageTelemetryConstants.Tags.Operation && (string?)t.Value == "read") &&
            m.Tags.Any(t => t.Key == StorageTelemetryConstants.Tags.DbSystem && (string?)t.Value == "postgresql") &&
            m.Tags.Any(t => t.Key == StorageTelemetryConstants.Tags.Result && (string?)t.Value == "success") &&
            !m.Tags.Any(t => t.Key == StorageTelemetryConstants.Tags.EntityType));
    }

    [Fact]
    public void record_error_should_increment_counter_with_error_tags()
    {
        var exception = new InvalidOperationException("test error");

        _metrics.RecordError("update", "sqlite", exception, "session");

        _recordedMetrics.ShouldContain(m =>
            m.Name == StorageTelemetryConstants.Instruments.OperationCount &&
            (long)m.Value! == 1 &&
            m.Tags.Any(t => t.Key == StorageTelemetryConstants.Tags.Operation && (string?)t.Value == "update") &&
            m.Tags.Any(t => t.Key == StorageTelemetryConstants.Tags.DbSystem && (string?)t.Value == "sqlite") &&
            m.Tags.Any(t => t.Key == StorageTelemetryConstants.Tags.EntityType && (string?)t.Value == "session") &&
            m.Tags.Any(t => t.Key == StorageTelemetryConstants.Tags.Result && (string?)t.Value == "error") &&
            m.Tags.Any(t => t.Key == StorageTelemetryConstants.Tags.ErrorType && (string?)t.Value == "InvalidOperationException"));
    }

    [Fact]
    public void record_error_without_entity_type_should_omit_entity_type_tag()
    {
        var exception = new TimeoutException();

        _metrics.RecordError("delete", "in_memory", exception, null);

        _recordedMetrics.ShouldContain(m =>
            m.Name == StorageTelemetryConstants.Instruments.OperationCount &&
            (long)m.Value! == 1 &&
            m.Tags.Any(t => t.Key == StorageTelemetryConstants.Tags.Result && (string?)t.Value == "error") &&
            m.Tags.Any(t => t.Key == StorageTelemetryConstants.Tags.ErrorType && (string?)t.Value == "TimeoutException")
            && m.Tags.All(t => t.Key != StorageTelemetryConstants.Tags.EntityType));
    }

    [Fact]
    public void record_duration_should_record_histogram_with_correct_tags()
    {
        _metrics.RecordDuration("query", 1.234, "postgresql", "success", "token");

        _recordedMetrics.ShouldContain(m =>
            m.Name == StorageTelemetryConstants.Instruments.OperationDuration &&
            (double)m.Value! == 1.234 &&
            m.Tags.Any(t => t.Key == StorageTelemetryConstants.Tags.Operation && (string?)t.Value == "query") &&
            m.Tags.Any(t => t.Key == StorageTelemetryConstants.Tags.DbSystem && (string?)t.Value == "postgresql") &&
            m.Tags.Any(t => t.Key == StorageTelemetryConstants.Tags.EntityType && (string?)t.Value == "token") &&
            m.Tags.Any(t => t.Key == StorageTelemetryConstants.Tags.Result && (string?)t.Value == "success"));
    }

    [Fact]
    public void record_duration_without_entity_type_should_omit_entity_type_tag()
    {
        _metrics.RecordDuration("batch", 0.567, "mssql", "error", null);

        _recordedMetrics.ShouldContain(m =>
            m.Name == StorageTelemetryConstants.Instruments.OperationDuration &&
            (double)m.Value! == 0.567 &&
            m.Tags.Any(t => t.Key == StorageTelemetryConstants.Tags.Operation && (string?)t.Value == "batch") &&
            m.Tags.Any(t => t.Key == StorageTelemetryConstants.Tags.DbSystem && (string?)t.Value == "mssql") &&
            m.Tags.Any(t => t.Key == StorageTelemetryConstants.Tags.Result && (string?)t.Value == "error")
            && m.Tags.All(t => t.Key != StorageTelemetryConstants.Tags.EntityType));
    }
}
