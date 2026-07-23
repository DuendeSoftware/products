// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Hosting;
using Duende.Storage.Internal;
using Duende.Storage.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UnitTests.Common;

namespace UnitTests.Hosting;

public class StoragePurgeHostTests
{
    private readonly IdentityServerOptions _options = new();
    private readonly ILogger<StoragePurgeHost> _logger = TestLogger.Create<StoragePurgeHost>();

    [Fact]
    public async Task disabled_should_not_start()
    {
        _options.StoragePurge.EnablePurge = false;

        var factory = CreateStoreFactory();
        var host = new StoragePurgeHost(factory, _options, _logger);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        // StartAsync returns Task.CompletedTask when disabled
        await host.StartAsync(cts.Token);
        await host.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task run_purge_against_empty_store_completes_without_error()
    {
        _options.StoragePurge.EnablePurge = true;
        _options.StoragePurge.BatchSize = 100;

        var factory = CreateStoreFactory();
        var host = new StoragePurgeHost(factory, _options, _logger);

        var exception = await Record.ExceptionAsync(async () =>
        {
            await host.RunPurgeAsync(CancellationToken.None);
        });

        exception.ShouldBeNull();
    }

    [Fact]
    public async Task run_purge_disabled_should_short_circuit()
    {
        _options.StoragePurge.EnablePurge = false;

        var factory = CreateStoreFactory();
        var host = new StoragePurgeHost(factory, _options, _logger);

        var exception = await Record.ExceptionAsync(async () =>
        {
            await host.RunPurgeAsync(CancellationToken.None);
        });

        exception.ShouldBeNull();
    }

    [Fact]
    public async Task run_purge_should_survive_store_factory_exception()
    {
        _options.StoragePurge.EnablePurge = true;

        var factory = new ThrowingStoreFactory();
        var host = new StoragePurgeHost(factory, _options, _logger);

        var exception = await Record.ExceptionAsync(async () =>
        {
            await host.RunPurgeAsync(CancellationToken.None);
        });

        exception.ShouldBeNull();
    }

    [Fact]
    public async Task run_purge_respects_cancellation()
    {
        _options.StoragePurge.EnablePurge = true;

        var factory = CreateStoreFactory();
        var host = new StoragePurgeHost(factory, _options, _logger);

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var exception = await Record.ExceptionAsync(async () =>
        {
            await host.RunPurgeAsync(cts.Token);
        });

        exception.ShouldBeNull();
    }

    [Fact]
    public async Task run_purge_clamps_invalid_batch_size()
    {
        _options.StoragePurge.EnablePurge = true;
        _options.StoragePurge.BatchSize = 0; // Below minimum — should be clamped to 1

        var factory = CreateStoreFactory();
        var host = new StoragePurgeHost(factory, _options, _logger);

        var exception = await Record.ExceptionAsync(async () =>
        {
            await host.RunPurgeAsync(CancellationToken.None);
        });

        exception.ShouldBeNull();
    }

    [Fact]
    public async Task run_purge_clamps_oversized_batch()
    {
        _options.StoragePurge.EnablePurge = true;
        _options.StoragePurge.BatchSize = 5000; // Above maximum — should be clamped to 1000

        var factory = CreateStoreFactory();
        var host = new StoragePurgeHost(factory, _options, _logger);

        var exception = await Record.ExceptionAsync(async () =>
        {
            await host.RunPurgeAsync(CancellationToken.None);
        });

        exception.ShouldBeNull();
    }

    private static IStoreFactory CreateStoreFactory()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var dbName = $"purge_test_{Guid.NewGuid():N}";
        services.AddStorageInternal(storage =>
            storage.AddSqliteStore(opt =>
                opt.ConnectionString = $"Data Source={dbName};Mode=Memory;Cache=Shared"));

        var sp = services.BuildServiceProvider();
        var pooledStore = sp.GetRequiredService<IPooledStore>();
        ((Duende.Storage.Schema.IDatabaseSchema)pooledStore).MigrateAsync(CancellationToken.None).GetAwaiter().GetResult();

        return new SimpleStoreFactory(pooledStore.OpenPool(0));
    }

    private sealed class SimpleStoreFactory(IStore store) : IStoreFactory
    {
        public Task<IStore> GetStore(CancellationToken _) => Task.FromResult(store);
    }

    private sealed class ThrowingStoreFactory : IStoreFactory
    {
        public Task<IStore> GetStore(CancellationToken _) => throw new InvalidOperationException("Simulated factory failure");
    }
}
