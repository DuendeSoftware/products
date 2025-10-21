// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Documentation.Mcp.Infrastructure;

internal class TemporaryFileStream : FileStream
{
    public static TemporaryFileStream Create()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            Guid.NewGuid() + ".tmp");

        return new TemporaryFileStream(path);
    }

    public static async Task<TemporaryFileStream> CreateFromAsync(Stream otherStream)
    {
        var temporaryFileStream = Create();

        await otherStream.CopyToAsync(temporaryFileStream);
        temporaryFileStream.Position = 0;

        return temporaryFileStream;
    }

    private readonly string _path;

    private TemporaryFileStream(string path)
        : base(path, FileMode.OpenOrCreate, FileAccess.ReadWrite) => _path = path;

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        try
        {
            File.Delete(_path);
        }
        catch
        {
            // Best-effort...
        }
    }
}
