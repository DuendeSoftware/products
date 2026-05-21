// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Diagnostics.Metrics;

namespace Duende.Storage.Internal.Telemetry;

/// <summary>
/// Records storage metrics using System.Diagnostics.Metrics.
/// </summary>
public sealed class StorageMetrics : IDisposable
{
    private readonly Meter _meter;
    private readonly Counter<long> _operationCounter;
    private readonly Histogram<double> _operationDuration;

    public StorageMetrics()
    {
        _meter = new Meter(StorageTelemetryConstants.MeterName, StorageTracing.ServiceVersion);
        _operationCounter = _meter.CreateCounter<long>(
            StorageTelemetryConstants.Instruments.OperationCount,
            description: "Number of storage operations executed.");
        _operationDuration = _meter.CreateHistogram<double>(
            StorageTelemetryConstants.Instruments.OperationDuration,
            unit: "s",
            description: "Duration of storage operations in seconds.");
    }

    public void RecordSuccess(string operation, string dbSystem, string? entityType)
    {
        if (entityType != null)
        {
            _operationCounter.Add(1,
                new(StorageTelemetryConstants.Tags.Operation, operation),
                new(StorageTelemetryConstants.Tags.DbSystem, dbSystem),
                new(StorageTelemetryConstants.Tags.EntityType, entityType),
                new(StorageTelemetryConstants.Tags.Result, StorageTelemetryConstants.TagValues.Success));
        }
        else
        {
            _operationCounter.Add(1,
                new(StorageTelemetryConstants.Tags.Operation, operation),
                new(StorageTelemetryConstants.Tags.DbSystem, dbSystem),
                new(StorageTelemetryConstants.Tags.Result, StorageTelemetryConstants.TagValues.Success));
        }
    }

    public void RecordError(string operation, string dbSystem, Exception ex, string? entityType)
    {
        if (entityType != null)
        {
            _operationCounter.Add(1,
                new(StorageTelemetryConstants.Tags.Operation, operation),
                new(StorageTelemetryConstants.Tags.DbSystem, dbSystem),
                new(StorageTelemetryConstants.Tags.EntityType, entityType),
                new(StorageTelemetryConstants.Tags.Result, StorageTelemetryConstants.TagValues.Error),
                new(StorageTelemetryConstants.Tags.ErrorType, ex.GetType().Name));
        }
        else
        {
            _operationCounter.Add(1,
                new(StorageTelemetryConstants.Tags.Operation, operation),
                new(StorageTelemetryConstants.Tags.DbSystem, dbSystem),
                new(StorageTelemetryConstants.Tags.Result, StorageTelemetryConstants.TagValues.Error),
                new(StorageTelemetryConstants.Tags.ErrorType, ex.GetType().Name));
        }
    }

    public void RecordDuration(string operation, double durationSeconds, string dbSystem, string result, string? entityType)
    {
        if (entityType != null)
        {
            _operationDuration.Record(durationSeconds,
                new(StorageTelemetryConstants.Tags.Operation, operation),
                new(StorageTelemetryConstants.Tags.DbSystem, dbSystem),
                new(StorageTelemetryConstants.Tags.EntityType, entityType),
                new(StorageTelemetryConstants.Tags.Result, result));
        }
        else
        {
            _operationDuration.Record(durationSeconds,
                new(StorageTelemetryConstants.Tags.Operation, operation),
                new(StorageTelemetryConstants.Tags.DbSystem, dbSystem),
                new(StorageTelemetryConstants.Tags.Result, result));
        }
    }

    public void Dispose() => _meter.Dispose();
}
