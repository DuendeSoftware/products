// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer;
using Duende.IdentityServer.Infrastructure.DistributedLock;
using Duende.IdentityServer.Services.KeyManagement;
using Duende.Storage;
using Duende.Storage.Schema;
using Duende.Storage.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace UnitTests.Configuration.DependencyInjection;

public class DistributedLockRegistrationTests
{
    private static (IServiceCollection Services, IIdentityServerBuilder Builder) CreateBuilder()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var builder = services.AddIdentityServer();
        return (services, builder);
    }

    private static async Task<ServiceProvider> BuildAndMigrateAsync(IServiceCollection services)
    {
        var sp = services.BuildServiceProvider();
        await sp.GetRequiredService<IStorageInstanceSchema>().MigrateAsync(Ct.None);
        return sp;
    }

    /// <summary>
    /// The service resolved for a non-enumerable, non-keyed dependency is always the <b>last</b>
    /// matching descriptor, so a plain <c>Add</c> after an earlier <c>TryAdd</c> wins even though both
    /// descriptors remain in the collection. This asserts that, rather than assuming only one
    /// descriptor exists.
    /// </summary>
    private static ServiceDescriptor LastDistributedLockDescriptor(IServiceCollection services) =>
        services.Last(d => d.ServiceType == typeof(IDistributedLock));

    [Fact]
    public async Task No_storage_resolves_in_process_lock()
    {
        var (services, _) = CreateBuilder();
        await using var sp = services.BuildServiceProvider();

        sp.GetRequiredService<IDistributedLock>().ShouldBeOfType<InProcessDistributedLock>();
    }

    [Fact]
    public async Task AddStorage_default_resolves_storage_lock()
    {
        var (services, builder) = CreateBuilder();
        builder.AddStorage(_ => _.AddSqliteInMemory());
        await using var sp = await BuildAndMigrateAsync(services);

        sp.GetRequiredService<IDistributedLock>().ShouldBeOfType<StorageDistributedLock>();
    }

    [Fact]
    public async Task AddDistributedStorageLock_called_before_AddIdentityServer_still_resolves_storage_lock()
    {
        // AddDistributedStorageLock() (the internal helper AddStorage()/AddSpaces() call) registers
        // IDistributedLock with a plain Add before AddIdentityServer()'s TryAddSingleton runs. The
        // plain Add must still win (it's the later descriptor the container resolves), proving the
        // registration is order-independent.
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddDistributedStorageLock();
        var builder = services.AddIdentityServer();
        builder.AddStorage(_ => _.AddSqliteInMemory());

        await using var sp = await BuildAndMigrateAsync(services);

        sp.GetRequiredService<IDistributedLock>().ShouldBeOfType<StorageDistributedLock>();
    }

    [Fact]
    public async Task AddStorage_named_instance_only_resolves_in_process_lock()
    {
        var (services, builder) = CreateBuilder();
        var named = StorageInstanceId.Create("named");
        builder.AddStorage(named, storage => storage.AddSqlite(o => o.ConnectionString = $"Data Source=distlock_{Guid.NewGuid():N};Mode=Memory;Cache=Shared"));
        await using var sp = services.BuildServiceProvider();

        sp.GetRequiredService<IDistributedLock>().ShouldBeOfType<InProcessDistributedLock>();
    }

    [Fact]
    public async Task AddSpaces_resolves_storage_lock()
    {
        var (services, builder) = CreateBuilder();
        builder.AddSpaces(_ => { });
        await using var sp = services.BuildServiceProvider();

        sp.GetRequiredService<IDistributedLock>().ShouldBeOfType<StorageDistributedLock>();
    }

    [Fact]
    public async Task AddStorage_default_then_AddSpaces_resolves_storage_lock_and_the_type_registry_builds()
    {
        var (services, builder) = CreateBuilder();
        builder.AddStorage(_ => _.AddSqliteInMemory());
        builder.AddSpaces(_ => { });

        // AddStorage() and AddSpaces() both call AddDistributedStorageLock(), which is idempotent:
        // DistributedLockDso.V1 must be registered exactly once, or building/migrating below throws
        // (DataStorageTypeRegistry rejects a duplicate entity type id).
        await using var sp = await BuildAndMigrateAsync(services);

        sp.GetRequiredService<IDistributedLock>().ShouldBeOfType<StorageDistributedLock>();
    }

    [Fact]
    public async Task AddSpaces_then_AddStorage_default_resolves_storage_lock()
    {
        var (services, builder) = CreateBuilder();
        builder.AddSpaces(_ => { });
        builder.AddStorage(_ => _.AddSqliteInMemory());
        await using var sp = await BuildAndMigrateAsync(services);

        sp.GetRequiredService<IDistributedLock>().ShouldBeOfType<StorageDistributedLock>();
    }

    [Fact]
    public void No_ImplementationFactory_descriptor_for_IDistributedLock_or_IKeyManager()
    {
        var (services, builder) = CreateBuilder();
        builder.AddStorage(_ => _.AddSqliteInMemory());
        builder.AddSpaces(_ => { });

        services.Any(d => d.ServiceType == typeof(IDistributedLock) && d.ImplementationFactory is not null)
            .ShouldBeFalse();
        services.Any(d => d.ServiceType == typeof(IKeyManager) && d.ImplementationFactory is not null)
            .ShouldBeFalse();
    }

    [Fact]
    public async Task StorageDistributedLock_is_registered_by_type_and_disposed_with_the_provider()
    {
        var (services, builder) = CreateBuilder();
        builder.AddStorage(_ => _.AddSqliteInMemory());

        var descriptor = LastDistributedLockDescriptor(services);
        descriptor.ImplementationInstance.ShouldBeNull();
        descriptor.ImplementationType.ShouldBe(typeof(StorageDistributedLock));

        var sp = await BuildAndMigrateAsync(services);
        var distributedLock = (StorageDistributedLock)sp.GetRequiredService<IDistributedLock>();

        await sp.DisposeAsync();

        // StorageDistributedLock.TryAcquireAsync returns null at once once the lock itself has been
        // disposed (its _disposed flag is set), without even reaching the store. Resolving null here,
        // on the very instance the disposed provider handed out, proves the container called
        // DisposeAsync on it.
        var afterDispose = await distributedLock.TryAcquireAsync(
            LockName.OutboxProcessor, TimeSpan.Zero, LeaseTiming.Default, Ct.None);

        afterDispose.ShouldBeNull();
    }

    /// <summary>
    /// Regression: <see cref="StorageDistributedLock"/> is a DI singleton, and many apps/tests dispose
    /// their <see cref="ServiceProvider"/> synchronously (<c>using var sp = ...BuildServiceProvider()</c>).
    /// Before it implemented <see cref="IDisposable"/> too, that threw "type only implements
    /// IAsyncDisposable". This must not throw once <c>AddStorage(default)</c> is in the collection.
    /// </summary>
    [Fact]
    public async Task AddStorage_default_provider_disposed_synchronously_does_not_throw()
    {
        var (services, builder) = CreateBuilder();
        builder.AddStorage(_ => _.AddSqliteInMemory());

        var sp = await BuildAndMigrateAsync(services);
        sp.GetRequiredService<IDistributedLock>().ShouldBeOfType<StorageDistributedLock>();

        Should.NotThrow(sp.Dispose);
    }
}
