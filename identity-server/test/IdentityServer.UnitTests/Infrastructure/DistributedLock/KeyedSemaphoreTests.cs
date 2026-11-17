// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
#nullable enable

using Duende.IdentityServer.Infrastructure.DistributedLock;

namespace IdentityServer.UnitTests.Infrastructure.DistributedLock;

public class KeyedSemaphoreTests
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    private readonly KeyedSemaphore _subject = new();

    [Fact]
    public async Task held_key_times_out_and_leaves_no_entry_after_release()
    {
        var lease = await _subject.TryEnterAsync("a", TimeSpan.FromSeconds(30), _ct);
        lease.ShouldNotBeNull();

        var second = await _subject.TryEnterAsync("a", TimeSpan.Zero, _ct);
        second.ShouldBeNull();

        await lease.DisposeAsync();
        _subject.Count.ShouldBe(0);
    }

    [Fact]
    public async Task different_keys_do_not_block_each_other()
    {
        var a = await _subject.TryEnterAsync("a", TimeSpan.FromSeconds(30), _ct);
        a.ShouldNotBeNull();

        var b = await _subject.TryEnterAsync("b", TimeSpan.Zero, _ct);

        b.ShouldNotBeNull();
        await b.DisposeAsync();
        await a.DisposeAsync();
    }

    [Fact]
    public async Task waiter_acquires_after_release_and_still_excludes_others()
    {
        var lease1 = await _subject.TryEnterAsync("a", TimeSpan.FromSeconds(30), _ct);
        lease1.ShouldNotBeNull();

        var waiter = _subject.TryEnterAsync("a", TimeSpan.FromSeconds(30), _ct);
        await lease1.DisposeAsync();
        var lease2 = await waiter.WaitAsync(TimeSpan.FromSeconds(30), _ct);

        lease2.ShouldNotBeNull();
        var third = await _subject.TryEnterAsync("a", TimeSpan.Zero, _ct);
        third.ShouldBeNull();

        await lease2.DisposeAsync();
        _subject.Count.ShouldBe(0);
    }

    [Fact]
    public async Task cancelled_waiter_throws_and_leaves_no_entry()
    {
        var holder = await _subject.TryEnterAsync("a", TimeSpan.FromSeconds(30), _ct);
        holder.ShouldNotBeNull();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(_ct);

        var waiter = _subject.TryEnterAsync("a", TimeSpan.FromSeconds(30), cts.Token);
        await cts.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => waiter);
        await holder.DisposeAsync();
        _subject.Count.ShouldBe(0);

        var next = await _subject.TryEnterAsync("a", TimeSpan.Zero, _ct);
        next.ShouldNotBeNull();
        await next.DisposeAsync();
    }

    [Fact]
    public async Task disposing_a_lease_twice_releases_once()
    {
        var lease1 = await _subject.TryEnterAsync("a", TimeSpan.FromSeconds(30), _ct);
        lease1.ShouldNotBeNull();
        await lease1.DisposeAsync();
        await lease1.DisposeAsync();

        var lease2 = await _subject.TryEnterAsync("a", TimeSpan.FromSeconds(30), _ct);
        lease2.ShouldNotBeNull();

        var third = await _subject.TryEnterAsync("a", TimeSpan.Zero, _ct);
        third.ShouldBeNull();
        await lease2.DisposeAsync();
    }

    [Theory]
    [InlineData(-2)]
    public async Task negative_timeout_other_than_infinite_throws(int seconds) =>
        await Should.ThrowAsync<ArgumentOutOfRangeException>(
            () => _subject.TryEnterAsync("a", TimeSpan.FromSeconds(seconds), _ct));

    [Fact]
    public async Task timeout_larger_than_int_max_milliseconds_throws() =>
        await Should.ThrowAsync<ArgumentOutOfRangeException>(
            () => _subject.TryEnterAsync("a", TimeSpan.FromMilliseconds(int.MaxValue).Add(TimeSpan.FromMilliseconds(1)), _ct));

    [Fact]
    public async Task infinite_timeout_is_allowed()
    {
        var lease = await _subject.TryEnterAsync("a", Timeout.InfiniteTimeSpan, _ct);
        lease.ShouldNotBeNull();
        await lease.DisposeAsync();
    }
}
