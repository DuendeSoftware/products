// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.IdentityServer.Internal.Saml.Infrastructure;

internal class LimitedReadStream(Stream innerStream, long maxBytes) : Stream
{
    private long _bytesRead;

    public override void Flush() => innerStream.Flush();

    public override int Read(byte[] buffer, int offset, int count)
    {
        var bytesToRead = (int)Math.Min(count, maxBytes - _bytesRead);
        if (bytesToRead <= 0)
        {
            throw new InvalidOperationException("Maximum stream size exceeded.");
        }

        var read = innerStream.Read(buffer, offset, bytesToRead);
        _bytesRead += read;
        return read;
    }

    public override long Seek(long offset, SeekOrigin origin) => innerStream.Seek(offset, origin);

    public override void SetLength(long value) => innerStream.SetLength(value);

    public override void Write(byte[] buffer, int offset, int count) => innerStream.Write(buffer, offset, count);

    public override bool CanRead => innerStream.CanRead;

    public override bool CanSeek => innerStream.CanSeek;

    public override bool CanWrite => innerStream.CanWrite;

    public override long Length => innerStream.Length;

    public override long Position
    {
        get => innerStream.Position;
        set => innerStream.Position = value;
    }

    public override async ValueTask DisposeAsync()
    {
        await innerStream.DisposeAsync();
        await base.DisposeAsync();
    }

    protected override void Dispose(bool disposing)
    {
        innerStream.Dispose();
        base.Dispose(disposing);
    }
}
