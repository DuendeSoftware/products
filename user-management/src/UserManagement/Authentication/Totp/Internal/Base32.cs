// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Duende.UserManagement.Authentication.Totp.Internal;

// https://datatracker.ietf.org/doc/html/rfc4648#section-6
internal static class Base32
{
    private const int BitsPerByte = 8;
    private const int BitsPerChar = 5;
    private const string TextAlphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    internal static string Encode(IReadOnlyCollection<byte> bytes)
    {
        if (bytes.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder((int)Math.Ceiling(bytes.Count / (double)BitsPerChar) * BitsPerByte);
        var buffer = 0;
        var bufferBitCount = 0;
        foreach (var b in bytes)
        {
            // shift the byte into the right of the buffer
            buffer = (buffer << BitsPerByte) | b;
            bufferBitCount += BitsPerByte;

            // while the buffer contains enough bits for a char
            while (bufferBitCount >= BitsPerChar)
            {
                // get only the bits for the first char
                var charIndex = buffer >> (bufferBitCount - BitsPerChar);

                // record the char
                _ = builder.Append(TextAlphabet[charIndex]);

                // drop the bits for that char
                bufferBitCount -= BitsPerChar;
                buffer &= (1 << bufferBitCount) - 1;
            }
        }

        // trailing bits
        if (bufferBitCount > 0)
        {
            // shift them left to make up a full char
            var charIndex = (buffer << (BitsPerChar - bufferBitCount));
            _ = builder.Append(TextAlphabet[charIndex]);
        }

        // padding
        while (builder.Length % 8 != 0)
        {
            _ = builder.Append('=');
        }

        return builder.ToString();
    }

    internal static bool TryDecode(string input, [NotNullWhen(true)] out IReadOnlyCollection<byte>? result)
    {
        result = null;

        // padding
        var text = input.TrimEnd('=');

        var byteList = new List<byte>();
        var buffer = 0;
        var bufferBitCount = 0;
        foreach (var chr in text)
        {
            var charIndex = TextAlphabet.IndexOf(chr, StringComparison.OrdinalIgnoreCase);
            if (charIndex < 0)
            {
                return false;
            }

            // shift the char into the right of the buffer
            buffer = (buffer << BitsPerChar) | charIndex;
            bufferBitCount += BitsPerChar;

            // while the buffer contains enough bits for a byte
            while (bufferBitCount >= BitsPerByte)
            {
                // get only the bits for the first byte
                var b = (byte)(buffer >> (bufferBitCount - BitsPerByte));

                // record the byte
                byteList.Add(b);

                // drop the bits for that byte
                bufferBitCount -= BitsPerByte;
                buffer &= (1 << bufferBitCount) - 1;
            }
        }

        result = byteList;
        return true;
    }
}
