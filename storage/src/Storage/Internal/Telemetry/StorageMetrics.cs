// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Diagnostics.Metrics;

namespace Duende.Storage.Internal.Telemetry;

/// <summary>
/// Records storage metrics using System.Diagnostics.Metrics.
/// </summary>
/// <remarks>
/// This type is for usage by Duende Software products, is not supported for end user consumption, and not subject to semantic versioning rules.
/// </remarks>
internal sealed class StorageMetrics : IDisposable
{
    private readonly Meter _meter;
    private readonly Counter<long> _operationCounter;
    private readonly Histogram<double> _operationDuration;

    /// <summary>
    /// Initializes a new instance of <see cref="StorageMetrics"/>.
    /// </summary>
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

    /// <summary>
    /// Records a successful operation.
    /// </summary>
    /// <param name="operation">The operation name.</param>
    /// <param name="dbSystem">The database system.</param>
    /// <param name="entityType">The entity type name, or null.</param>
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

    /// <summary>
    /// Records a failed operation.
    /// </summary>
    /// <param name="operation">The operation name.</param>
    /// <param name="dbSystem">The database system.</param>
    /// <param name="ex">The exception that occurred.</param>
    /// <param name="entityType">The entity type name, or null.</param>
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

    /// <summary>
    /// Records operation duration.
    /// </summary>
    /// <param name="operation">The operation name.</param>
    /// <param name="durationSeconds">The duration in seconds.</param>
    /// <param name="dbSystem">The database system.</param>
    /// <param name="result">The result tag value (success or error).</param>
    /// <param name="entityType">The entity type name, or null.</param>
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

    /// <inheritdoc />
    public void Dispose() => _meter.Dispose();
}
