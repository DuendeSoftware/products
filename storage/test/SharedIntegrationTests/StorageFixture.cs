// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal;
using Duende.Storage.Schema;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.Storage.IntegrationTests;

public sealed class StorageFixture : IStorageFixture
{
    public delegate Task OnDisposeAction(StorageFixture fixture);

    public required ServiceProvider ServiceProvider { get; init; }
    public required IPartitionedStorage PartitionedStorage { get; init; }
    public required ICrossPartitionStorage DefaultCrossPartitionStorage { get; init; }
    public required IStorageInstanceSchema StorageInstanceSchema { get; init; }
    public required IPartitionedStorageFactory PartitionedStorageFactory { get; init; }
    public required ICrossPartitionStorageFactory CrossPartitionStorageFactory { get; init; }

    private OnDisposeAction? OnDispose { get; init; }

    public static DataCategoryName DataCategoryName { get; } = DataCategoryName.Create("not-important");

    public static async Task<StorageFixture> Create(
        Action<IStorageBuilder> configureStorage,
        Action<IServiceCollection>? configureServices = null,
        Func<IServiceProvider, Ct, Task>? beforeMigrate = null,
        OnDisposeAction? onDispose = null,
        Ct ct = default)
    {
        var services = new ServiceCollection();
        _ = services.AddLogging();
        configureServices?.Invoke(services);
        _ = services.AddStorageInternal(configureStorage);

        var provider = services.BuildServiceProvider();

        try
        {
            // Runs before migrations so providers can create external databases.
            if (beforeMigrate != null)
            {
                await beforeMigrate.Invoke(provider, ct);
            }

            var storageInstanceSchemaFactory = provider.GetRequiredService<IStorageInstanceSchemaFactory>();
            var storageInstanceRouter = provider.GetRequiredService<IStorageInstanceRouter>();
            foreach (var configuredStorageInstanceId in storageInstanceRouter.GetAll())
            {

                var configuredStorageInstanceSchema = await storageInstanceSchemaFactory.GetStorageInstanceSchema(configuredStorageInstanceId, ct);
                await configuredStorageInstanceSchema.MigrateAsync(ct);
            }

            var partitionedStorageFactory = provider.GetRequiredService<IPartitionedStorageFactory>();
            var crossPartitionStorageFactory = provider.GetRequiredService<ICrossPartitionStorageFactory>();
            var crossPartitionStorage = await crossPartitionStorageFactory.GetCrossPartitionStorageAsync(StorageInstanceId.Default, ct);
            var storageInstanceSchema = await storageInstanceSchemaFactory.GetStorageInstanceSchema(ct);
            var partitionedStorage = await partitionedStorageFactory.GetPartitionedStorageAsync(DataCategoryName, ct);

            return new StorageFixture()
            {
                ServiceProvider = provider,
                PartitionedStorageFactory = partitionedStorageFactory,
                StorageInstanceSchema = storageInstanceSchema,
                DefaultCrossPartitionStorage = crossPartitionStorage,
                PartitionedStorage = partitionedStorage,
                CrossPartitionStorageFactory = crossPartitionStorageFactory,
                OnDispose = onDispose
            };
        }
        catch
        {
            await provider.DisposeAsync();
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (OnDispose != null)
        {
            await OnDispose.Invoke(this);
        }
        await ServiceProvider.DisposeAsync();
    }
}

/// <summary>
/// Abstraction for creating a storage fixture in provider-agnostic integration tests.
/// Each provider implements this to wire up its own DI and database.
/// </summary>
public interface IStorageFixtureFactory
{
    /// <summary>
    /// Creates a fresh storage fixture backed by a new database.
    /// The returned fixture must be disposed to clean up the database.
    /// </summary>
    Task<IStorageFixture> CreateAsync(CancellationToken ct, Action<IServiceCollection>? configureServices = null);
    void AddStorageInstance(IStorageBuilder storage);
}

/// <summary>
/// A disposable storage fixture that exposes the <see cref="IPartitionedStorage"/> under test.
/// </summary>
public interface IStorageFixture : IAsyncDisposable
{
    ServiceProvider ServiceProvider { get; }
    IPartitionedStorage PartitionedStorage { get; }
    ICrossPartitionStorage DefaultCrossPartitionStorage { get; }
    IStorageInstanceSchema StorageInstanceSchema { get; }
    IPartitionedStorageFactory PartitionedStorageFactory { get; }
    ICrossPartitionStorageFactory CrossPartitionStorageFactory { get; }
}
