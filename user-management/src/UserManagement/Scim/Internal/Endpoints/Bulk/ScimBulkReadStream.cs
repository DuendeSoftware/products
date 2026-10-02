// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Scim.Internal.Endpoints.Bulk;

internal sealed class ScimBulkReadStream : Stream
{
    private static readonly object PayloadTooLargeMarker = new();

    private readonly Stream _innerStream;
    private readonly bool _leaveOpen;
    private readonly long _maxBytes;
    private long _bytesRead;
    private bool _limitExceeded;

    internal ScimBulkReadStream(Stream innerStream, long maxBytes, bool leaveOpen)
    {
        ArgumentNullException.ThrowIfNull(innerStream);
        ArgumentOutOfRangeException.ThrowIfNegative(maxBytes);

        _innerStream = innerStream;
        _maxBytes = maxBytes;
        _leaveOpen = leaveOpen;
    }

    public override bool CanRead => _innerStream.CanRead;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    internal bool LimitExceeded => _limitExceeded;

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override void Flush() => _innerStream.Flush();

    public override Task FlushAsync(CancellationToken cancellationToken) =>
        _innerStream.FlushAsync(cancellationToken);

    public override int Read(byte[] buffer, int offset, int count) =>
        Read(buffer.AsSpan(offset, count));

    public override int Read(Span<byte> buffer)
    {
        ThrowIfLimitExceeded();

        if (buffer.IsEmpty)
        {
            return _innerStream.Read(buffer);
        }

        var remaining = _maxBytes - _bytesRead;
        var bytesToRead = (int)Math.Min(buffer.Length, remaining + 1);
        var read = _innerStream.Read(buffer[..bytesToRead]);
        RecordBytesRead(read, remaining);
        return read;
    }

    public override Task<int> ReadAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken) =>
        ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    public override async ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        ThrowIfLimitExceeded();

        if (buffer.IsEmpty)
        {
            return await _innerStream.ReadAsync(buffer, cancellationToken);
        }

        var remaining = _maxBytes - _bytesRead;
        var bytesToRead = (int)Math.Min(buffer.Length, remaining + 1);
        var read = await _innerStream.ReadAsync(buffer[..bytesToRead], cancellationToken);
        RecordBytesRead(read, remaining);
        return read;
    }

    public override long Seek(long offset, SeekOrigin origin) =>
        throw new NotSupportedException();

    public override void SetLength(long value) =>
        throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_leaveOpen)
        {
            _innerStream.Dispose();
        }

        base.Dispose(disposing);
    }

    internal static bool IsPayloadTooLarge(InvalidOperationException exception) =>
        exception.Data.Contains(PayloadTooLargeMarker);

    private void RecordBytesRead(int read, long remaining)
    {
        _bytesRead += read;
        if (read > remaining)
        {
            _limitExceeded = true;
            ThrowIfLimitExceeded();
        }
    }

    private void ThrowIfLimitExceeded()
    {
        if (_limitExceeded)
        {
            var exception = new InvalidOperationException("The maximum SCIM bulk payload size was exceeded.");
            exception.Data[PayloadTooLargeMarker] = true;
            throw exception;
        }
    }
}
