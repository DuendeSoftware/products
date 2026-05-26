// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.IO.Compression;

namespace Duende.UserManagement.Authentication.QrCodes.Internal;

/// <summary>
/// A minimal PNG encoder that produces a valid single-channel (grayscale) PNG byte stream.
/// </summary>
internal static class PngEncoder
{
    // Standard PNG file signature (8 bytes).
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    // Chunk type bytes (ASCII).
    private static readonly byte[] IhdrType = [0x49, 0x48, 0x44, 0x52]; // "IHDR"
    private static readonly byte[] IdatType = [0x49, 0x44, 0x41, 0x54]; // "IDAT"
    private static readonly byte[] IendType = [0x49, 0x45, 0x4E, 0x44]; // "IEND"

    // Pre-computed CRC32 table using the standard polynomial 0xEDB88320.
    private static readonly uint[] Crc32Table = BuildCrc32Table();

    /// <summary>
    /// Encodes a grayscale pixel array as a valid PNG byte stream.
    /// </summary>
    /// <param name="width">Image width in pixels.</param>
    /// <param name="height">Image height in pixels.</param>
    /// <param name="pixels">
    /// Row-major grayscale pixel data, <paramref name="width"/> * <paramref name="height"/> bytes.
    /// 0x00 = black, 0xFF = white.
    /// </param>
    /// <returns>A complete PNG file as a byte array.</returns>
    internal static byte[] Encode(int width, int height, byte[] pixels)
    {
        var idatData = BuildIdatData(width, height, pixels);

        // Compute total output length:
        //   signature(8) + IHDR chunk(4+4+13+4=25) + IDAT chunk(4+4+idatData.Length+4) + IEND chunk(4+4+0+4=12)
        int outputLength;
        checked
        {
            outputLength = 8 + 25 + (4 + 4 + idatData.Length + 4) + 12;
        }

        var output = new byte[outputLength];
        var pos = 0;

        // PNG signature
        WriteBytes(output, ref pos, PngSignature);

        // IHDR chunk: 13 bytes of data
        var ihdrData = BuildIhdrData(width, height);
        WriteChunk(output, ref pos, IhdrType, ihdrData);

        // IDAT chunk
        WriteChunk(output, ref pos, IdatType, idatData);

        // IEND chunk (no data)
        WriteChunk(output, ref pos, IendType, []);

        return output;
    }

    // Builds the 13-byte IHDR data block.
    private static byte[] BuildIhdrData(int width, int height)
    {
        var data = new byte[13];
        var pos = 0;
        WriteUInt32BE(data, ref pos, (uint)width);
        WriteUInt32BE(data, ref pos, (uint)height);
        data[pos++] = 8;   // bit depth
        data[pos++] = 0;   // color type: grayscale
        data[pos++] = 0;   // compression method: deflate
        data[pos++] = 0;   // filter method: adaptive
        data[pos++] = 0;   // interlace method: none
        return data;
    }

    // Builds the complete IDAT payload: zlib header + deflate-compressed filtered rows + Adler-32.
    private static byte[] BuildIdatData(int width, int height, byte[] pixels)
    {
        // Build the filtered scanline data: one filter byte (0x00 = None) per row followed by the row pixels.
        int filteredLength;
        checked
        {
            filteredLength = height * (1 + width);
        }
        var filtered = new byte[filteredLength];
        for (var row = 0; row < height; row++)
        {
            var dst = row * (1 + width);
            filtered[dst] = 0x00; // filter type: None
            Array.Copy(pixels, row * width, filtered, dst + 1, width);
        }

        // Compute Adler-32 over the uncompressed filtered data (required by zlib).
        var adler = ComputeAdler32(filtered);

        // Deflate-compress the filtered data.
        byte[] deflated;
        using (var ms = new MemoryStream())
        {
            using (var ds = new DeflateStream(ms, CompressionLevel.Optimal, leaveOpen: true))
            {
                ds.Write(filtered, 0, filtered.Length);
            }
            deflated = ms.ToArray();
        }

        // Assemble: zlib header (2 bytes) + deflate bytes + Adler-32 (4 bytes, big-endian).
        var idatData = new byte[2 + deflated.Length + 4];
        var pos = 0;
        idatData[pos++] = 0x78; // zlib CMF: deflate, window size 32 KB
        idatData[pos++] = 0x01; // zlib FLG: no dict, lowest compression (check bits make 0x7801 % 31 == 0)
        Array.Copy(deflated, 0, idatData, pos, deflated.Length);
        pos += deflated.Length;
        WriteUInt32BE(idatData, ref pos, adler);

        return idatData;
    }

    // Writes a PNG chunk: length(4) + type(4) + data + crc(4).
    private static void WriteChunk(byte[] output, ref int pos, byte[] type, byte[] data)
    {
        WriteUInt32BE(output, ref pos, (uint)data.Length);
        WriteBytes(output, ref pos, type);
        WriteBytes(output, ref pos, data);

        // CRC32 over type + data: accumulate both segments before finalising.
        var running = RunCrc32(0xFFFFFFFF, type, 0, type.Length);
        running = RunCrc32(running, data, 0, data.Length);
        WriteUInt32BE(output, ref pos, running ^ 0xFFFFFFFF);
    }

    // Adler-32 checksum per RFC 1950.
    private static uint ComputeAdler32(byte[] data)
    {
        const uint mod = 65521;
        uint s1 = 1, s2 = 0;
        foreach (var b in data)
        {
            s1 = (s1 + b) % mod;
            s2 = (s2 + s1) % mod;
        }
        return (s2 << 16) | s1;
    }

    // Builds the 256-entry CRC32 lookup table using polynomial 0xEDB88320.
    private static uint[] BuildCrc32Table()
    {
        var table = new uint[256];
        for (var i = 0; i < 256; i++)
        {
            var c = (uint)i;
            for (var k = 0; k < 8; k++)
            {
                c = (c & 1) != 0 ? (0xEDB88320u ^ (c >> 1)) : (c >> 1);
            }
            table[i] = c;
        }
        return table;
    }

    // Computes CRC32 over a byte range, initialising from 0xFFFFFFFF.
    internal static uint ComputeCrc32(byte[] data, int offset, int length)
        => RunCrc32(0xFFFFFFFF, data, offset, length) ^ 0xFFFFFFFF;

    // Accumulates CRC32 over a byte range into a running value WITHOUT finalising (no XOR at end).
    // Start with 0xFFFFFFFF, then XOR the final result with 0xFFFFFFFF when done.
    private static uint RunCrc32(uint crc, byte[] data, int offset, int length)
    {
        for (var i = offset; i < offset + length; i++)
        {
            crc = Crc32Table[(crc ^ data[i]) & 0xFF] ^ (crc >> 8);
        }
        return crc;
    }

    private static void WriteUInt32BE(byte[] buffer, ref int pos, uint value)
    {
        buffer[pos++] = (byte)(value >> 24);
        buffer[pos++] = (byte)(value >> 16);
        buffer[pos++] = (byte)(value >> 8);
        buffer[pos++] = (byte)value;
    }

    private static void WriteBytes(byte[] buffer, ref int pos, byte[] source)
    {
        Array.Copy(source, 0, buffer, pos, source.Length);
        pos += source.Length;
    }
}
