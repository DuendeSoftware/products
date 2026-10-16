// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Diagnostics.Metrics;
using Duende.Storage.Internal.Telemetry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;

namespace Duende.Storage.Sqlite;

public sealed class StorageMetricsTests : IDisposable
{
    private readonly ServiceProvider _provider;
    private readonly StorageMetrics _metrics;
    private readonly MetricCollector<long> _counts;
    private readonly MetricCollector<double> _durations;

    public StorageMetricsTests()
    {
        _provider = new ServiceCollection().AddMetrics().BuildServiceProvider();
        var meterFactory = _provider.GetRequiredService<IMeterFactory>();
        _metrics = new StorageMetrics(meterFactory);
        _counts = new MetricCollector<long>(meterFactory, StorageTelemetryConstants.MeterName, StorageTelemetryConstants.Instruments.OperationCount);
        _durations = new MetricCollector<double>(meterFactory, StorageTelemetryConstants.MeterName, StorageTelemetryConstants.Instruments.OperationDuration);
    }

    public void Dispose()
    {
        _counts.Dispose();
        _durations.Dispose();
        _provider.Dispose();
    }

    private static bool HasTag<T>(CollectedMeasurement<T> measurement, string key, string value) where T : struct =>
        measurement.Tags.TryGetValue(key, out var actual) && (string?)actual == value;

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

        _counts.GetMeasurementSnapshot().ShouldContain(m =>
            m.Value == 1 &&
            HasTag(m, StorageTelemetryConstants.Tags.Operation, "create") &&
            HasTag(m, StorageTelemetryConstants.Tags.DbSystem, "mssql") &&
            HasTag(m, StorageTelemetryConstants.Tags.EntityType, "client") &&
            HasTag(m, StorageTelemetryConstants.Tags.Result, "success"));
    }

    [Fact]
    public void record_success_without_entity_type_should_omit_entity_type_tag()
    {
        _metrics.RecordSuccess("read", "postgresql", null);

        _counts.GetMeasurementSnapshot().ShouldContain(m =>
            m.Value == 1 &&
            HasTag(m, StorageTelemetryConstants.Tags.Operation, "read") &&
            HasTag(m, StorageTelemetryConstants.Tags.DbSystem, "postgresql") &&
            HasTag(m, StorageTelemetryConstants.Tags.Result, "success") &&
            !m.Tags.ContainsKey(StorageTelemetryConstants.Tags.EntityType));
    }

    [Fact]
    public void record_error_should_increment_counter_with_error_tags()
    {
        var exception = new InvalidOperationException("test error");

        _metrics.RecordError("update", "sqlite", exception, "session");

        _counts.GetMeasurementSnapshot().ShouldContain(m =>
            m.Value == 1 &&
            HasTag(m, StorageTelemetryConstants.Tags.Operation, "update") &&
            HasTag(m, StorageTelemetryConstants.Tags.DbSystem, "sqlite") &&
            HasTag(m, StorageTelemetryConstants.Tags.EntityType, "session") &&
            HasTag(m, StorageTelemetryConstants.Tags.Result, "error") &&
            HasTag(m, StorageTelemetryConstants.Tags.ErrorType, "InvalidOperationException"));
    }

    [Fact]
    public void record_error_without_entity_type_should_omit_entity_type_tag()
    {
        var exception = new TimeoutException();

        _metrics.RecordError("delete", "in_memory", exception, null);

        _counts.GetMeasurementSnapshot().ShouldContain(m =>
            m.Value == 1 &&
            HasTag(m, StorageTelemetryConstants.Tags.Result, "error") &&
            HasTag(m, StorageTelemetryConstants.Tags.ErrorType, "TimeoutException")
            && !m.Tags.ContainsKey(StorageTelemetryConstants.Tags.EntityType));
    }

    [Fact]
    public void record_duration_should_record_histogram_with_correct_tags()
    {
        _metrics.RecordDuration("query", 1.234, "postgresql", "success", "token");

        _durations.GetMeasurementSnapshot().ShouldContain(m =>
            m.Value == 1.234 &&
            HasTag(m, StorageTelemetryConstants.Tags.Operation, "query") &&
            HasTag(m, StorageTelemetryConstants.Tags.DbSystem, "postgresql") &&
            HasTag(m, StorageTelemetryConstants.Tags.EntityType, "token") &&
            HasTag(m, StorageTelemetryConstants.Tags.Result, "success"));
    }

    [Fact]
    public void record_duration_without_entity_type_should_omit_entity_type_tag()
    {
        _metrics.RecordDuration("batch", 0.567, "mssql", "error", null);

        _durations.GetMeasurementSnapshot().ShouldContain(m =>
            m.Value == 0.567 &&
            HasTag(m, StorageTelemetryConstants.Tags.Operation, "batch") &&
            HasTag(m, StorageTelemetryConstants.Tags.DbSystem, "mssql") &&
            HasTag(m, StorageTelemetryConstants.Tags.Result, "error")
            && !m.Tags.ContainsKey(StorageTelemetryConstants.Tags.EntityType));
    }
}
