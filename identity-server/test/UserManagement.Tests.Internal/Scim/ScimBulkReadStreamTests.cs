// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.UserManagement.Scim.Internal.Endpoints.Bulk;

namespace Duende.Platform.UserManagement.Scim;

public sealed class ScimBulkReadStreamTests
{
    [Fact]
    public async Task should_share_the_limit_across_sync_and_async_reads()
    {
        await using var inner = new MemoryStream([1, 2, 3, 4]);
        await using var subject = new ScimBulkReadStream(inner, 3, leaveOpen: true);
        var buffer = new byte[2];

        subject.Read(buffer, 0, buffer.Length).ShouldBe(2);

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            async () => await subject.ReadExactlyAsync(
                buffer,
                TestContext.Current.CancellationToken));

        ScimBulkReadStream.IsPayloadTooLarge(exception).ShouldBeTrue();
    }

    [Fact]
    public async Task should_keep_rejecting_reads_after_the_limit_is_exceeded()
    {
        await using var inner = new MemoryStream([1, 2]);
        await using var subject = new ScimBulkReadStream(inner, 1, leaveOpen: true);

        _ = await Should.ThrowAsync<InvalidOperationException>(
            async () => await subject.ReadExactlyAsync(
                new byte[2],
                TestContext.Current.CancellationToken));

        var exception = Should.Throw<InvalidOperationException>(
            () => subject.Read(Array.Empty<byte>(), 0, 0));

        ScimBulkReadStream.IsPayloadTooLarge(exception).ShouldBeTrue();
    }

    [Fact]
    public async Task should_leave_the_inner_stream_open_when_disposed()
    {
        await using var inner = new MemoryStream([1]);
        var subject = new ScimBulkReadStream(inner, 1, leaveOpen: true);

        await subject.DisposeAsync();

        inner.CanRead.ShouldBeTrue();
    }

    [Fact]
    public async Task should_honor_cancellation()
    {
        await using var inner = new MemoryStream([1]);
        await using var subject = new ScimBulkReadStream(inner, 1, leaveOpen: true);
        using var cancellationSource = new CancellationTokenSource();
        await cancellationSource.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(
            async () => await subject.ReadExactlyAsync(new byte[1], cancellationSource.Token));
    }
}
