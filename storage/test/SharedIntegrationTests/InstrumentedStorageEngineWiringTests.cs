// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Duende.Storage.Internal;
using Duende.Storage.Internal.Builder;
using Duende.Storage.Internal.Operations;
using Duende.Storage.Internal.Querying.SearchFields;
using Duende.Storage.Internal.Telemetry;
using Duende.Storage.Schema;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;

namespace Duende.Storage.IntegrationTests;

public partial class InstrumentedStorageEngineWiringTests
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    private Task<IStorageFixture> CreateProviderAsync() =>
        FixtureFactory.CreateAsync(_ct, services => services.AddDsoRegistration<TestDso>());

    private static MetricCollector<long> CreateCollector(IStorageFixture fixture) =>
        new(fixture.ServiceProvider.GetRequiredService<IMeterFactory>(),
            StorageTelemetryConstants.MeterName,
            StorageTelemetryConstants.Instruments.OperationCount);

    private static ActivityListener ListenForActivitiesInTrace(ActivityTraceId traceId, ConcurrentBag<Activity> activities) =>
        new()
        {
            ShouldListenTo = source => source.Name == StorageTracing.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity =>
            {
                if (activity.TraceId == traceId)
                {
                    activities.Add(activity);
                }
            }
        };

    private Task<CreateResult> CreateAsync(IPartitionedStorage storage) =>
        storage.CreateAsync(UuidV7.New(), new TestDso($"wired-{Guid.NewGuid()}"), [], SearchFieldCollection.Empty, Expiration.NoExpiration, [], _ct);

    [Fact]
    public async Task Create_through_keyed_partitioned_storage_records_metric_and_activity()
    {
        await using var fixture = await CreateProviderAsync();
        using var collector = CreateCollector(fixture);
        using var testActivity = new Activity(nameof(Create_through_keyed_partitioned_storage_records_metric_and_activity)).Start();
        var activities = new ConcurrentBag<Activity>();
        using var listener = ListenForActivitiesInTrace(testActivity.TraceId, activities);
        ActivitySource.AddActivityListener(listener);

        var storage = fixture.ServiceProvider.GetRequiredKeyedService<IPartitionedStorage>(StorageInstanceId.Default);
        var result = await CreateAsync(storage);

        result.ShouldBe(CreateResult.Success);
        var measurement = collector.GetMeasurementSnapshot().ShouldHaveSingleItem();
        measurement.Value.ShouldBe(1);
        measurement.Tags[StorageTelemetryConstants.Tags.Operation].ShouldBe(StorageTelemetryConstants.Operations.Create);
        measurement.Tags[StorageTelemetryConstants.Tags.DbSystem].ShouldBe(ExpectedDbSystem);
        measurement.Tags[StorageTelemetryConstants.Tags.Result].ShouldBe(StorageTelemetryConstants.TagValues.Success);

        var activity = activities.ShouldHaveSingleItem();
        activity.OperationName.ShouldBe("Store.Create");
        activity.GetTagItem(StorageTelemetryConstants.Tags.DbSystem).ShouldBe(ExpectedDbSystem);
    }

    [Fact]
    public async Task Purge_expired_through_keyed_cross_partition_storage_records_metric()
    {
        await using var fixture = await CreateProviderAsync();
        using var collector = CreateCollector(fixture);

        var storage = fixture.ServiceProvider.GetRequiredKeyedService<ICrossPartitionStorage>(StorageInstanceId.Default);
        _ = await storage.PurgeExpiredAsync(10, _ct);

        var measurement = collector.GetMeasurementSnapshot().ShouldHaveSingleItem();
        measurement.Tags[StorageTelemetryConstants.Tags.Operation].ShouldBe(StorageTelemetryConstants.Operations.PurgeExpired);
        measurement.Tags[StorageTelemetryConstants.Tags.DbSystem].ShouldBe(ExpectedDbSystem);
    }

    [Fact]
    public async Task Create_through_storage_from_factory_records_metric()
    {
        await using var fixture = await CreateProviderAsync();
        using var collector = CreateCollector(fixture);

        var storage = await fixture.PartitionedStorageFactory.GetPartitionedStorageAsync(StorageFixture.DataCategoryName, _ct);
        var result = await CreateAsync(storage);

        result.ShouldBe(CreateResult.Success);
        var measurement = collector.GetMeasurementSnapshot().ShouldHaveSingleItem();
        measurement.Tags[StorageTelemetryConstants.Tags.Operation].ShouldBe(StorageTelemetryConstants.Operations.Create);
        measurement.Tags[StorageTelemetryConstants.Tags.DbSystem].ShouldBe(ExpectedDbSystem);
        measurement.Tags[StorageTelemetryConstants.Tags.Result].ShouldBe(StorageTelemetryConstants.TagValues.Success);
    }

    [Fact]
    public async Task Migrating_through_keyed_schema_records_no_storage_metrics()
    {
        await using var fixture = await CreateProviderAsync();
        using var collector = CreateCollector(fixture);

        var schema = fixture.ServiceProvider.GetRequiredKeyedService<IStorageInstanceSchema>(StorageInstanceId.Default);
        await schema.MigrateAsync(_ct);

        collector.GetMeasurementSnapshot().ShouldBeEmpty();
    }
}
