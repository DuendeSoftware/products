// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System.Diagnostics;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Infrastructure.DistributedLock;
using Duende.IdentityServer.IntegrationTests.TestFramework;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services.KeyManagement;
using Duende.IdentityServer.Stores;
using Duende.IdentityServer.Stores.Storage.SigningKeys;
using Duende.Spaces;
using Duende.Storage.Sqlite;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.IdentityServer.IntegrationTests.Hosting.Coordination;

/// <summary>
/// Reproduces duplicate signing key creation across two nodes: two <see cref="ServiceProvider"/>
/// "nodes", each with Spaces and Storage-backed signing keys, pointed at one shared temp-file
/// SQLite database. Today's key manager locks only in-process (one lock instance per node), so
/// two nodes creating keys for the same Space at the same time can both decide no key exists yet
/// and both store one.
/// </summary>
public sealed class MultiNodeSigningKeyTests
{
    private const int CallersPerNode = 5;

    private readonly Ct _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task two_nodes_creating_keys_for_the_same_space_concurrently_create_only_one_key_per_algorithm()
    {
        await using var cluster = MultiNodeTestCluster.Create();
        var gate = new ConcurrentCreationGate(window: TimeSpan.FromSeconds(2));

        var nodeA = cluster.AddNode(services => ConfigureNode(services, cluster, decorateStore: s => DecorateSigningKeyStoreWithGates(s, [gate])));
        var nodeB = cluster.AddNode(services => ConfigureNode(services, cluster, decorateStore: s => DecorateSigningKeyStoreWithGates(s, [gate])));

        await cluster.MigrateAsync(_ct);

        var spaceId = await CreateSpaceAsync(nodeA, "Multi-node Signing Keys", "https://multi-node-signing-keys.example.com", _ct);

        // Only now does the gate start blocking StoreKeyAsync calls for this Space: space
        // creation itself must not be gated.
        gate.Space = spaceId;

        var callers = Enumerable.Range(0, CallersPerNode)
            .SelectMany(_ => new[] { nodeA, nodeB })
            .Select(node => RequestCurrentKeysAsync(node, spaceId, _ct));

        // StorageDistributedLock's busy-retry backoff runs on the cluster's shared FakeTimeProvider,
        // which never advances on its own. Pump it in real time while callers race for the lock, so a
        // caller that finds the lock busy doesn't wait forever on a frozen clock.
        using var tickerCts = CancellationTokenSource.CreateLinkedTokenSource(_ct);
        var ticker = PumpFakeTimeAsync(cluster.TimeProvider, tickerCts.Token);
        try
        {
            await Task.WhenAll(callers).WaitAsync(TimeSpan.FromSeconds(30), _ct);
        }
        finally
        {
            await StopPumpAsync(tickerCts, ticker);
        }

        var storedKeys = await LoadKeysAsync(nodeA, spaceId, _ct);
        AssertOneKeyPerAlgorithm(storedKeys, gate.SecondEntrantArrived);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public async Task nodes_creating_keys_for_two_spaces_concurrently_create_only_one_key_per_space_per_algorithm(int nodeCount)
    {
        await using var cluster = MultiNodeTestCluster.Create();
        var gateA = new ConcurrentCreationGate(window: TimeSpan.FromSeconds(2));
        var gateB = new ConcurrentCreationGate(window: TimeSpan.FromSeconds(2));

        var nodes = Enumerable.Range(0, nodeCount)
            .Select(_ => cluster.AddNode(services => ConfigureNode(services, cluster, decorateStore: s => DecorateSigningKeyStoreWithGates(s, [gateA, gateB]))))
            .ToArray();

        await cluster.MigrateAsync(_ct);

        var spaceAId = await CreateSpaceAsync(nodes[0], "Multi-node Signing Keys A", "https://multi-node-signing-keys-a.example.com", _ct);
        var spaceBId = await CreateSpaceAsync(nodes[0], "Multi-node Signing Keys B", "https://multi-node-signing-keys-b.example.com", _ct);

        gateA.Space = spaceAId;
        gateB.Space = spaceBId;

        var callers = nodes
            .SelectMany(node => Enumerable.Range(0, CallersPerNode).SelectMany(_ => new[]
            {
                RequestCurrentKeysAsync(node, spaceAId, _ct),
                RequestCurrentKeysAsync(node, spaceBId, _ct)
            }));

        using var tickerCts = CancellationTokenSource.CreateLinkedTokenSource(_ct);
        var ticker = PumpFakeTimeAsync(cluster.TimeProvider, tickerCts.Token);
        try
        {
            await Task.WhenAll(callers).WaitAsync(TimeSpan.FromSeconds(30), _ct);
        }
        finally
        {
            await StopPumpAsync(tickerCts, ticker);
        }

        AssertOneKeyPerAlgorithm(await LoadKeysAsync(nodes[0], spaceAId, _ct), gateA.SecondEntrantArrived);
        AssertOneKeyPerAlgorithm(await LoadKeysAsync(nodes[0], spaceBId, _ct), gateB.SecondEntrantArrived);

        // A Space creating keys must not block the other Space's creation: both gates must have
        // been able to proceed within the test's bounded window.
        spaceAId.ShouldNotBe(spaceBId);
    }

    [Fact]
    public async Task a_waiting_node_returns_the_holders_key_without_creating_another()
    {
        await using var cluster = MultiNodeTestCluster.Create();
        var pause = new PausedStore();
        var storeCallCounter = new StoreCallCounter();

        var nodeA = cluster.AddNode(services => ConfigureNode(services, cluster, decorateStore: s => DecorateSigningKeyStoreWithPause(s, pause)));
        var nodeB = cluster.AddNode(services => ConfigureNode(services, cluster, decorateStore: s => DecorateSigningKeyStoreWithCounter(s, storeCallCounter)));

        await cluster.MigrateAsync(_ct);

        var spaceId = await CreateSpaceAsync(nodeA, "Waiter Re-read", "https://waiter-re-read.example.com", _ct);

        var requestA = RequestCurrentKeysAsync(nodeA, spaceId, _ct);
        await pause.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10), _ct);

        var requestB = RequestCurrentKeysAsync(nodeB, spaceId, _ct);

        using var tickerCts = CancellationTokenSource.CreateLinkedTokenSource(_ct);
        var ticker = PumpFakeTimeAsync(cluster.TimeProvider, tickerCts.Token);
        try
        {
            // Give node B a moment to actually start waiting on node A's held lock before releasing it.
            await Task.Delay(TimeSpan.FromMilliseconds(200), _ct);
            pause.Release.TrySetResult();

            await Task.WhenAll(requestA, requestB).WaitAsync(TimeSpan.FromSeconds(30), _ct);
        }
        finally
        {
            pause.Release.TrySetResult();
            await StopPumpAsync(tickerCts, ticker);
        }

        var stored = await LoadKeysAsync(nodeA, spaceId, _ct);
        stored.Count.ShouldBe(1, "only the holder may create a key; the waiter must reuse it");
        storeCallCounter.Count.ShouldBe(0, "a waiting node that re-reads the store must not create its own key");
    }

    [Fact]
    public async Task on_timeout_a_waiting_node_returns_the_holders_already_stored_key_without_creating_another()
    {
        await using var cluster = MultiNodeTestCluster.Create();

        var nodeA = cluster.AddNode(services => ConfigureNode(services, cluster));
        var nodeB = cluster.AddNode(services => ConfigureNode(
            services,
            cluster,
            decorateStore: s => DecorateSigningKeyStoreToHideTheFirstRead(s),
            configureKeyManagement: options => options.KeyCreationLockTimeout = TimeSpan.FromMilliseconds(200)));

        await cluster.MigrateAsync(_ct);

        var spaceId = await CreateSpaceAsync(nodeA, "Timeout Re-read", "https://timeout-re-read.example.com", _ct);

        // Node A creates the Space's key uncontended.
        await RequestCurrentKeysAsync(nodeA, spaceId, _ct);
        var storedBefore = await LoadKeysAsync(nodeA, spaceId, _ct);
        storedBefore.Count.ShouldBe(1);

        // Node A now holds the lock directly (bypassing its KeyManager), so node B can never acquire it.
        await using var scope = nodeA.CreateAsyncScope();
        using var spaceScope = scope.ServiceProvider.GetRequiredService<ISpaceContextAccessor>().SetSpace(spaceId);
        var heldLock = scope.ServiceProvider.GetRequiredService<IDistributedLock>();
        await using var lease = await heldLock.TryAcquireAsync(LockName.SigningKeys(spaceId), TimeSpan.Zero, _ct);
        lease.ShouldNotBeNull("node A must hold the lock for the test to force node B to time out");

        using var tickerCts = CancellationTokenSource.CreateLinkedTokenSource(_ct);
        var ticker = PumpFakeTimeAsync(cluster.TimeProvider, tickerCts.Token);
        IReadOnlyCollection<KeyContainer> returnedKeys;
        try
        {
            returnedKeys = await RequestAndReturnCurrentKeysAsync(nodeB, spaceId, _ct).WaitAsync(TimeSpan.FromSeconds(10), _ct);
        }
        finally
        {
            await StopPumpAsync(tickerCts, ticker);
        }

        returnedKeys.ShouldHaveSingleItem().Id.ShouldBe(storedBefore.Single().Id);

        var storedAfter = await LoadKeysAsync(nodeA, spaceId, _ct);
        storedAfter.Count.ShouldBe(1, "node B must not create another key after timing out");
    }

    [Fact]
    public async Task on_timeout_with_no_stored_key_a_waiting_node_throws()
    {
        await using var cluster = MultiNodeTestCluster.Create();

        var nodeA = cluster.AddNode(services => ConfigureNode(services, cluster));
        var nodeB = cluster.AddNode(services => ConfigureNode(
            services,
            cluster,
            configureKeyManagement: options => options.KeyCreationLockTimeout = TimeSpan.FromMilliseconds(200)));

        await cluster.MigrateAsync(_ct);

        var spaceId = await CreateSpaceAsync(nodeA, "Timeout No Key", "https://timeout-no-key.example.com", _ct);

        // Node A holds the lock directly without ever creating a key.
        await using var scope = nodeA.CreateAsyncScope();
        using var spaceScope = scope.ServiceProvider.GetRequiredService<ISpaceContextAccessor>().SetSpace(spaceId);
        var heldLock = scope.ServiceProvider.GetRequiredService<IDistributedLock>();
        await using var lease = await heldLock.TryAcquireAsync(LockName.SigningKeys(spaceId), TimeSpan.Zero, _ct);
        lease.ShouldNotBeNull("node A must hold the lock for the test to force node B to time out");

        using var tickerCts = CancellationTokenSource.CreateLinkedTokenSource(_ct);
        var ticker = PumpFakeTimeAsync(cluster.TimeProvider, tickerCts.Token);
        try
        {
            _ = await Should.ThrowAsync<InvalidOperationException>(
                () => RequestAndReturnCurrentKeysAsync(nodeB, spaceId, _ct).WaitAsync(TimeSpan.FromSeconds(10), _ct));
        }
        finally
        {
            await StopPumpAsync(tickerCts, ticker);
        }
    }

    [Fact]
    public async Task no_node_waits_InitializationSynchronizationDelay_when_the_lock_is_distributed()
    {
        await using var cluster = MultiNodeTestCluster.Create();
        var nodeA = cluster.AddNode(services => ConfigureNode(
            services,
            cluster,
            configureKeyManagement: options => options.InitializationSynchronizationDelay = TimeSpan.FromHours(1)));

        await cluster.MigrateAsync(_ct);

        var spaceId = await CreateSpaceAsync(nodeA, "No Sync Delay", "https://no-sync-delay.example.com", _ct);

        var sw = Stopwatch.StartNew();
        await RequestCurrentKeysAsync(nodeA, spaceId, _ct);
        sw.Stop();

        sw.Elapsed.ShouldBeLessThan(
            TimeSpan.FromSeconds(10),
            "a distributed lock must skip InitializationSynchronizationDelay entirely, even though it is set to an hour");
    }

    [Fact]
    public async Task two_nodes_without_spaces_creating_keys_concurrently_create_only_one_key_per_algorithm()
    {
        await using var cluster = MultiNodeTestCluster.Create();
        var gate = new ConcurrentCreationGate(window: TimeSpan.FromSeconds(2)) { Space = SpaceId.Default };

        var nodeA = cluster.AddNode(services => ConfigureStorageOnlyNode(services, cluster, s => DecorateSigningKeyStoreWithGates(s, [gate])));
        var nodeB = cluster.AddNode(services => ConfigureStorageOnlyNode(services, cluster, s => DecorateSigningKeyStoreWithGates(s, [gate])));

        await cluster.MigrateAsync(_ct);

        var callers = Enumerable.Range(0, CallersPerNode)
            .SelectMany(_ => new[] { nodeA, nodeB })
            .Select(node => node.GetRequiredService<IKeyManager>().GetCurrentKeysAsync(_ct));

        using var tickerCts = CancellationTokenSource.CreateLinkedTokenSource(_ct);
        var ticker = PumpFakeTimeAsync(cluster.TimeProvider, tickerCts.Token);
        try
        {
            await Task.WhenAll(callers).WaitAsync(TimeSpan.FromSeconds(30), _ct);
        }
        finally
        {
            await StopPumpAsync(tickerCts, ticker);
        }

        var storedKeys = await nodeA.GetRequiredService<ISigningKeyStore>().LoadKeysAsync(_ct);
        storedKeys.Count.ShouldBe(storedKeys.Select(k => k.Algorithm).Distinct().Count(), "one key total across both nodes");
        AssertOneKeyPerAlgorithm(storedKeys, gate.SecondEntrantArrived);
    }

    private static Task RequestCurrentKeysAsync(ServiceProvider node, SpaceId spaceId, Ct ct) =>
        RequestAndReturnCurrentKeysAsync(node, spaceId, ct);

    private static async Task<IReadOnlyCollection<KeyContainer>> RequestAndReturnCurrentKeysAsync(ServiceProvider node, SpaceId spaceId, Ct ct)
    {
        var accessor = node.GetRequiredService<ISpaceContextAccessor>();
        using (accessor.SetSpace(spaceId))
        {
            return await node.GetRequiredService<IKeyManager>().GetCurrentKeysAsync(ct);
        }
    }

    private static async Task<IReadOnlyCollection<SerializedKey>> LoadKeysAsync(ServiceProvider node, SpaceId spaceId, Ct ct)
    {
        using var scope = node.GetRequiredService<ISpaceContextAccessor>().SetSpace(spaceId);
        return await node.GetRequiredService<ISigningKeyStore>().LoadKeysAsync(ct);
    }

    private static void AssertOneKeyPerAlgorithm(IReadOnlyCollection<SerializedKey> storedKeys, bool secondEntrantArrived)
    {
        var keysByAlgorithm = storedKeys.GroupBy(k => k.Algorithm).ToArray();

        keysByAlgorithm.ShouldAllBe(
            group => group.Count() == 1,
            $"exactly one key per configured signing algorithm must exist for the Space " +
            $"(a second entrant into StoreKeyAsync for this Space {(secondEntrantArrived ? "did" : "did not")} arrive)");
    }

    private static async Task<SpaceId> CreateSpaceAsync(ServiceProvider node, string name, string origin, Ct ct)
    {
        var created = await node.GetRequiredService<ISpaceAdmin>().CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = name,
                MatchPatterns = [new SpaceMatchPattern { Origin = origin }]
            },
            ct);
        created.IsSuccess.ShouldBeTrue($"space creation failed: {created}");
        return created.Id!.Value;
    }

    private static async Task PumpFakeTimeAsync(FakeTimeProvider timeProvider, Ct ct)
    {
        try
        {
            while (true)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(10), ct);
                timeProvider.Advance(TimeSpan.FromMilliseconds(100));
            }
        }
        catch (OperationCanceledException)
        {
            // expected: the caller cancels this once all callers have finished.
        }
    }

    private static async Task StopPumpAsync(CancellationTokenSource tickerCts, Task ticker)
    {
        await tickerCts.CancelAsync();
        try
        {
            await ticker;
        }
        catch (OperationCanceledException)
        {
            // expected: the ticker runs until cancelled above.
        }
    }

    private static void ConfigureNode(
        IServiceCollection services,
        MultiNodeTestCluster cluster,
        Action<IServiceCollection>? decorateStore = null,
        Action<KeyManagementOptions>? configureKeyManagement = null)
    {
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(cluster.KeyRingDirectory));

        var builder = services.AddIdentityServer(options =>
        {
            options.KeyManagement.InitializationSynchronizationDelay = TimeSpan.Zero;
            configureKeyManagement?.Invoke(options.KeyManagement);
        });

        builder
            .AddStorage(s => s.AddSqlite(o => o.ConnectionString = cluster.ConnectionString))
            .AddOperationalStorage()
            .AddSpaces(_ => { });

        SpacesTestLicense.RegisterEntitled(services);

        decorateStore?.Invoke(services);
    }

    private static void ConfigureStorageOnlyNode(
        IServiceCollection services,
        MultiNodeTestCluster cluster,
        Action<IServiceCollection>? decorateStore = null,
        Action<KeyManagementOptions>? configureKeyManagement = null)
    {
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());

        var builder = services.AddIdentityServer(options =>
        {
            options.KeyManagement.InitializationSynchronizationDelay = TimeSpan.Zero;
            configureKeyManagement?.Invoke(options.KeyManagement);
        });

        builder.AddStorage(s => s.AddSqlite(o => o.ConnectionString = cluster.ConnectionString))
            .AddOperationalStorage();
        builder.AddSigningKeyStore<SigningKeyStore>();

        decorateStore?.Invoke(services);
    }

    private static void DecorateSigningKeyStoreWithGates(IServiceCollection services, IReadOnlyList<ConcurrentCreationGate> gates)
    {
        var registration = services.Last(d => d.ServiceType == typeof(ISigningKeyStore));
        registration.ImplementationType.ShouldBe(typeof(SigningKeyStore));
        services.Remove(registration);
        services.Add(new ServiceDescriptor(
            typeof(ISigningKeyStore),
            sp => new GatedSigningKeyStore(
                ActivatorUtilities.CreateInstance<SigningKeyStore>(sp),
                gates,
                sp.GetService<ISpaceContextAccessor>()),
            registration.Lifetime));
    }

    private static void DecorateSigningKeyStoreWithPause(IServiceCollection services, PausedStore pause)
    {
        var registration = services.Last(d => d.ServiceType == typeof(ISigningKeyStore));
        registration.ImplementationType.ShouldBe(typeof(SigningKeyStore));
        services.Remove(registration);
        services.Add(new ServiceDescriptor(
            typeof(ISigningKeyStore),
            sp => new PausingSigningKeyStore(ActivatorUtilities.CreateInstance<SigningKeyStore>(sp), pause),
            registration.Lifetime));
    }

    private static void DecorateSigningKeyStoreWithCounter(IServiceCollection services, StoreCallCounter counter)
    {
        var registration = services.Last(d => d.ServiceType == typeof(ISigningKeyStore));
        registration.ImplementationType.ShouldBe(typeof(SigningKeyStore));
        services.Remove(registration);
        services.Add(new ServiceDescriptor(
            typeof(ISigningKeyStore),
            sp => new CountingSigningKeyStore(ActivatorUtilities.CreateInstance<SigningKeyStore>(sp), counter),
            registration.Lifetime));
    }

    private static void DecorateSigningKeyStoreToHideTheFirstRead(IServiceCollection services)
    {
        var registration = services.Last(d => d.ServiceType == typeof(ISigningKeyStore));
        registration.ImplementationType.ShouldBe(typeof(SigningKeyStore));
        services.Remove(registration);
        services.Add(new ServiceDescriptor(
            typeof(ISigningKeyStore),
            sp => new HideFirstReadSigningKeyStore(ActivatorUtilities.CreateInstance<SigningKeyStore>(sp)),
            registration.Lifetime));
    }

    /// <summary>
    /// Forces the race this test must catch, without assuming either outcome is deterministic by
    /// itself: the first call into <see cref="ISigningKeyStore.StoreKeyAsync"/> for <see cref="Space"/>
    /// waits until EITHER a second such call arrives concurrently (two nodes both decided, independently,
    /// that no key exists yet -- the bug a cross-node lock must prevent) OR a bounded real-time
    /// <paramref name="window"/> elapses. Without a cross-node lock, both nodes' <c>KeyManager</c>s can
    /// reach <c>StoreKeyAsync</c> at the same time: the second entrant arrives, both proceed immediately,
    /// and both store a key, so the assertion catches two keys for one algorithm. With the lock, only one
    /// node's <c>KeyManager</c> ever holds the creation lock for this Space, so no second entrant ever
    /// arrives; the lone entrant proceeds once the window elapses, and any other node's request is
    /// satisfied by re-reading the (by then stored) key instead of creating another one.
    /// </summary>
    private sealed class ConcurrentCreationGate(TimeSpan window)
    {
        private readonly TaskCompletionSource _secondEntrantArrived = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _entered;

        public SpaceId? Space { get; set; }

        /// <summary>
        /// Whether a second call ever arrived while the first was waiting. Diagnostic only: the
        /// contract this test enforces is the stored-key-count assertion, not this flag.
        /// </summary>
        public bool SecondEntrantArrived { get; private set; }

        public async Task EnterAsync(Ct ct)
        {
            if (Interlocked.Increment(ref _entered) >= 2)
            {
                SecondEntrantArrived = true;
                _secondEntrantArrived.TrySetResult();
            }

            await Task.WhenAny(_secondEntrantArrived.Task, Task.Delay(window, ct));
        }
    }

    private sealed class PausedStore
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class StoreCallCounter
    {
        private int _count;

        public int Count => _count;

        public void Increment() => Interlocked.Increment(ref _count);
    }

    private sealed class GatedSigningKeyStore(ISigningKeyStore inner, IReadOnlyList<ConcurrentCreationGate> gates, ISpaceContextAccessor? accessor) : ISigningKeyStore
    {
        public Task<IReadOnlyCollection<SerializedKey>> LoadKeysAsync(Ct ct) => inner.LoadKeysAsync(ct);

        public async Task StoreKeyAsync(SerializedKey key, Ct ct)
        {
            var spaceId = accessor?.GetSpaceIdOrDefault() ?? SpaceId.Default;
            var gate = gates.FirstOrDefault(g => g.Space == spaceId);
            if (gate != null)
            {
                await gate.EnterAsync(ct);
            }

            await inner.StoreKeyAsync(key, ct);
        }

        public Task DeleteKeyAsync(string id, Ct ct) => inner.DeleteKeyAsync(id, ct);
    }

    private sealed class PausingSigningKeyStore(ISigningKeyStore inner, PausedStore pause) : ISigningKeyStore
    {
        public Task<IReadOnlyCollection<SerializedKey>> LoadKeysAsync(Ct ct) => inner.LoadKeysAsync(ct);

        public async Task StoreKeyAsync(SerializedKey key, Ct ct)
        {
            pause.Entered.TrySetResult();
            await pause.Release.Task.WaitAsync(ct);
            await inner.StoreKeyAsync(key, ct);
        }

        public Task DeleteKeyAsync(string id, Ct ct) => inner.DeleteKeyAsync(id, ct);
    }

    private sealed class CountingSigningKeyStore(ISigningKeyStore inner, StoreCallCounter counter) : ISigningKeyStore
    {
        public Task<IReadOnlyCollection<SerializedKey>> LoadKeysAsync(Ct ct) => inner.LoadKeysAsync(ct);

        public Task StoreKeyAsync(SerializedKey key, Ct ct)
        {
            counter.Increment();
            return inner.StoreKeyAsync(key, ct);
        }

        public Task DeleteKeyAsync(string id, Ct ct) => inner.DeleteKeyAsync(id, ct);
    }

    /// <summary>
    /// Makes the first <see cref="LoadKeysAsync"/> call return empty, as if no key existed yet, so a
    /// <c>KeyManager</c> enters the lock-acquisition path even though the store already has a usable
    /// key. Every later call returns the real contents, so a timed-out waiter's re-read sees the key.
    /// </summary>
    private sealed class HideFirstReadSigningKeyStore(ISigningKeyStore inner) : ISigningKeyStore
    {
        private int _reads;

        public async Task<IReadOnlyCollection<SerializedKey>> LoadKeysAsync(Ct ct)
        {
            if (Interlocked.Increment(ref _reads) == 1)
            {
                return [];
            }

            return await inner.LoadKeysAsync(ct);
        }

        public Task StoreKeyAsync(SerializedKey key, Ct ct) => inner.StoreKeyAsync(key, ct);

        public Task DeleteKeyAsync(string id, Ct ct) => inner.DeleteKeyAsync(id, ct);
    }
}
