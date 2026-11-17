// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System.Diagnostics;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Infrastructure.DistributedLock;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Services.KeyManagement;
using Duende.IdentityServer.Stores;
using Duende.Spaces;
using Microsoft.Extensions.DependencyInjection;
using UnitTests.Validation.Setup;

namespace UnitTests.Services.Default.KeyManagement;

public class KeyManagerKeyCreationLockTests : IDisposable
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    private readonly MockSigningKeyStore _store = new();
    private readonly MockSigningKeyProtector _protector = new();
    private readonly RecordingDistributedLock _timeoutLock = new() { AcquireResult = _ => Task.FromResult<ILockLease?>(null) };
    private readonly ServiceProvider _provider;

    public KeyManagerKeyCreationLockTests() => _provider = BuildProvider(configureServices: null);

    public void Dispose() => _provider.Dispose();

    [Fact]
    public async Task GetCurrentKeysAsync_throws_and_creates_no_key_when_the_lock_cannot_be_acquired_and_store_has_no_usable_key()
    {
        using var provider = BuildProvider(services => services.AddSingleton<IDistributedLock>(_timeoutLock));
        using var scope = provider.CreateScope();
        var keyManager = scope.ServiceProvider.GetRequiredService<IKeyManager>();

        _ = await Should.ThrowAsync<Exception>(() => keyManager.GetCurrentKeysAsync(_ct));

        _store.Keys.ShouldBeEmpty("no key may be created without holding the lock");
    }

    [Fact]
    public async Task The_configured_KeyCreationLockTimeout_is_passed_to_the_lock()
    {
        var timeout = TimeSpan.FromSeconds(17);
        using var provider = BuildProvider(
            services => services.AddSingleton<IDistributedLock>(_timeoutLock),
            options => options.KeyManagement.KeyCreationLockTimeout = timeout);
        using var scope = provider.CreateScope();
        var keyManager = scope.ServiceProvider.GetRequiredService<IKeyManager>();

        _ = await Should.ThrowAsync<Exception>(() => keyManager.GetCurrentKeysAsync(_ct));

        _timeoutLock.WaitTimeouts.ShouldBe([timeout]);
    }

    [Fact]
    public async Task GetCurrentKeysAsync_honors_cancellation_while_waiting_for_the_lock()
    {
        using var provider = BuildProvider(services => services.AddSingleton<IDistributedLock>(new BlockingDistributedLock()));
        using var scope = provider.CreateScope();
        var keyManager = scope.ServiceProvider.GetRequiredService<IKeyManager>();

        using var cts = new CancellationTokenSource();
        var task = keyManager.GetCurrentKeysAsync(cts.Token);
        await cts.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(() => task);
    }

    [Fact]
    public async Task GetCurrentKeysAsync_returns_a_usable_store_key_without_creating_one_when_the_lock_times_out()
    {
        var container = new RsaKeyContainer(CryptoHelper.CreateRsaSecurityKey(), "RS256", DateTime.UtcNow);
        _store.Keys.Add(_protector.Protect(container));

        using var provider = BuildProvider(services => services.AddSingleton<IDistributedLock>(_timeoutLock));
        using var scope = provider.CreateScope();
        var keyManager = scope.ServiceProvider.GetRequiredService<IKeyManager>();

        var keys = await keyManager.GetCurrentKeysAsync(_ct);

        keys.ShouldHaveSingleItem().Id.ShouldBe(container.Id);
        _store.Keys.Count.ShouldBe(1, "a usable key from the store must be returned instead of creating a new one");
    }

    [Fact]
    public async Task GetCurrentKeysAsync_skips_InitializationSynchronizationDelay_when_the_lock_is_distributed()
    {
        var distributedLock = new RecordingDistributedLock { IsDistributed = true };
        using var provider = BuildProvider(
            services => services.AddSingleton<IDistributedLock>(distributedLock),
            options => options.KeyManagement.InitializationSynchronizationDelay = TimeSpan.FromSeconds(30));
        using var scope = provider.CreateScope();
        var keyManager = scope.ServiceProvider.GetRequiredService<IKeyManager>();

        var sw = Stopwatch.StartNew();
        await keyManager.GetCurrentKeysAsync(_ct);
        sw.Stop();

        sw.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(5), "a distributed lock must skip InitializationSynchronizationDelay entirely");
    }

    [Fact]
    public async Task GetCurrentKeysAsync_waits_InitializationSynchronizationDelay_with_the_in_process_lock()
    {
        var delay = TimeSpan.FromMilliseconds(200);
        using var provider = BuildProvider(
            services => services.AddSingleton<IDistributedLock>(new InProcessDistributedLock()),
            options => options.KeyManagement.InitializationSynchronizationDelay = delay);
        using var scope = provider.CreateScope();
        var keyManager = scope.ServiceProvider.GetRequiredService<IKeyManager>();

        var sw = Stopwatch.StartNew();
        await keyManager.GetCurrentKeysAsync(_ct);
        sw.Stop();

        sw.Elapsed.ShouldBeGreaterThanOrEqualTo(delay, "the in-process lock must still wait InitializationSynchronizationDelay");
    }

    [Fact]
    public async Task Lock_name_uses_the_default_space_without_a_space_context_accessor()
    {
        var recorder = new RecordingDistributedLock();
        using var provider = BuildProvider(services => services.AddSingleton<IDistributedLock>(recorder));
        using var scope = provider.CreateScope();
        var keyManager = scope.ServiceProvider.GetRequiredService<IKeyManager>();

        _ = await keyManager.GetCurrentKeysAsync(_ct);

        recorder.Names.ShouldHaveSingleItem().ShouldBe(LockName.SigningKeys(SpaceId.Default));
    }

    [Fact]
    public async Task Lock_name_uses_the_current_space_with_a_space_context_accessor()
    {
        var spaceId = SpaceId.New();
        var recorder = new RecordingDistributedLock();
        using var provider = BuildProvider(services =>
        {
            services.AddSingleton<IDistributedLock>(recorder);
            services.AddSingleton<ISpaceContextAccessor>(new FixedSpaceContextAccessor(spaceId));
        });
        using var scope = provider.CreateScope();
        var keyManager = scope.ServiceProvider.GetRequiredService<IKeyManager>();

        _ = await keyManager.GetCurrentKeysAsync(_ct);

        recorder.Names.ShouldHaveSingleItem().ShouldBe(LockName.SigningKeys(spaceId));
    }

    private ServiceProvider BuildProvider(
        Action<IServiceCollection>? configureServices,
        Action<IdentityServerOptions>? configureOptions = null)
    {
        var services = new ServiceCollection();
        _ = services.AddLogging();
        _ = services.AddIdentityServer(options =>
        {
            options.KeyManagement.InitializationSynchronizationDelay = TimeSpan.Zero;
            configureOptions?.Invoke(options);
        });
        _ = services.AddSingleton<ISigningKeyStore>(_store);
        _ = services.AddSingleton<ISigningKeyProtector>(_protector);
        _ = services.AddSingleton<IIssuerNameService>(new TestIssuerNameService());
        configureServices?.Invoke(services);
        return services.BuildServiceProvider();
    }

    private sealed class RecordingDistributedLock : IDistributedLock
    {
        public bool IsDistributed { get; init; }

        public List<TimeSpan> WaitTimeouts { get; } = [];

        public List<LockName> Names { get; } = [];

        public Func<LockName, Task<ILockLease?>>? AcquireResult { get; init; }

        public Task<ILockLease?> TryAcquireAsync(LockName name, TimeSpan waitTimeout, LeaseTiming timing, Ct ct)
        {
            Names.Add(name);
            WaitTimeouts.Add(waitTimeout);
            return AcquireResult?.Invoke(name) ?? Task.FromResult<ILockLease?>(new NoopLease());
        }

        private sealed class NoopLease : ILockLease
        {
            public Ct Lost => Ct.None;

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }

    private sealed class BlockingDistributedLock : IDistributedLock
    {
        public bool IsDistributed => false;

        public async Task<ILockLease?> TryAcquireAsync(LockName name, TimeSpan waitTimeout, LeaseTiming timing, Ct ct)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            return null;
        }
    }

    private sealed class FixedSpaceContextAccessor(SpaceId spaceId) : ISpaceContextAccessor
    {
        public SpaceId GetSpaceId() => spaceId;

        public bool IsSpaceIdConfigured() => true;

        public IDisposable SetSpace(SpaceId newSpaceId) => NoopDisposable.Instance;

        private sealed class NoopDisposable : IDisposable
        {
            public static NoopDisposable Instance { get; } = new();

            public void Dispose() { }
        }
    }
}
