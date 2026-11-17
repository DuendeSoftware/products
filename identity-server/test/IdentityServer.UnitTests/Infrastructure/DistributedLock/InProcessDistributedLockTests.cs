// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
#nullable enable

using Duende.IdentityServer.Infrastructure.DistributedLock;

namespace IdentityServer.UnitTests.Infrastructure.DistributedLock;

public class InProcessDistributedLockTests
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    private readonly InProcessDistributedLock _subject = new();

    [Fact]
    public void is_not_distributed() => _subject.IsDistributed.ShouldBeFalse();

    [Fact]
    public async Task mutual_exclusion_for_the_same_name()
    {
        var name = LockName.OutboxProcessor;

        var lease = await _subject.TryAcquireAsync(name, TimeSpan.FromSeconds(30), LeaseTiming.Default, _ct);
        lease.ShouldNotBeNull();

        var second = await _subject.TryAcquireAsync(name, TimeSpan.Zero, LeaseTiming.Default, _ct);
        second.ShouldBeNull();

        await lease.DisposeAsync();

        var third = await _subject.TryAcquireAsync(name, TimeSpan.Zero, LeaseTiming.Default, _ct);
        third.ShouldNotBeNull();
        await third.DisposeAsync();
    }

    [Fact]
    public async Task different_names_are_independent()
    {
        var a = await _subject.TryAcquireAsync(LockName.OutboxProcessor, TimeSpan.FromSeconds(30), LeaseTiming.Default, _ct);
        a.ShouldNotBeNull();

        var b = await _subject.TryAcquireAsync(LockName.StoragePurge, TimeSpan.Zero, LeaseTiming.Default, _ct);
        b.ShouldNotBeNull();

        await a.DisposeAsync();
        await b.DisposeAsync();
    }

    [Fact]
    public async Task lost_is_never_cancelled()
    {
        var lease = await _subject.TryAcquireAsync(LockName.OutboxProcessor, TimeSpan.FromSeconds(30), LeaseTiming.Default, _ct);
        lease.ShouldNotBeNull();

        lease.Lost.IsCancellationRequested.ShouldBeFalse();
        lease.Lost.ShouldBe(Ct.None);

        await lease.DisposeAsync();
    }

    [Fact]
    public async Task wait_timeout_zero_returns_null_immediately_when_held()
    {
        var name = LockName.ServerSideSessionCleanup;
        var holder = await _subject.TryAcquireAsync(name, TimeSpan.FromSeconds(30), LeaseTiming.Default, _ct);
        holder.ShouldNotBeNull();

        var result = await _subject.TryAcquireAsync(name, TimeSpan.Zero, LeaseTiming.Default, _ct);
        result.ShouldBeNull();

        await holder.DisposeAsync();
    }

    [Fact]
    public async Task timing_is_ignored()
    {
        var name = LockName.StoragePurge;
        var custom = new LeaseTiming(TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(1));

        var lease = await _subject.TryAcquireAsync(name, TimeSpan.Zero, custom, _ct);
        lease.ShouldNotBeNull();
        await Task.Delay(50, _ct);
        lease.Lost.IsCancellationRequested.ShouldBeFalse();

        await lease.DisposeAsync();
    }
}
