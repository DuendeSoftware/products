// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Authentication.QrCodes.Internal;

/// <summary>
/// Encodes raw byte data into QR data codewords for a specific version and
/// error correction level, following the encoding rules of ISO/IEC 18004.
/// </summary>
internal static class QrDataEncoder
{
    // Alphanumeric character-to-value lookup.
    // Characters outside the set are not expected (caller should detect mode first).
    private static readonly int[] AlphanumericValues = BuildAlphanumericTable();

    /// <summary>
    /// Encodes the given data into QR data codewords for the specified version and ECC level.
    /// </summary>
    /// <param name="data">The raw byte data to encode.</param>
    /// <param name="version">The QR code version (1-40).</param>
    /// <param name="ecc">The error correction level.</param>
    /// <returns>
    /// A byte array of length equal to the version's <see cref="QrVersionInfo.DataCodewords"/>,
    /// containing the encoded and padded data codewords.
    /// </returns>
    internal static byte[] Encode(ReadOnlySpan<byte> data, int version, QrEccLevel ecc)
    {
        var mode = QrModeDetector.Detect(data);
        var info = QrVersionTables.Get(version, ecc);
        var capacityBits = info.DataCodewords * 8;

        var buffer = new QrBitBuffer();

        // Mode indicator (4 bits)
        buffer.AppendBits(ModeIndicator(mode), 4);

        // Character count indicator
        var countBits = QrVersionSelector.GetCharacterCountBits(mode, version);
        buffer.AppendBits(data.Length, countBits);

        // Encoded data
        switch (mode)
        {
            case QrEncodingMode.Numeric:
                EncodeNumeric(buffer, data);
                break;
            case QrEncodingMode.Alphanumeric:
                EncodeAlphanumeric(buffer, data);
                break;
            case QrEncodingMode.Byte:
                EncodeByte(buffer, data);
                break;
        }

        // Terminator: up to 4 zero bits, but don't exceed capacity
        var terminatorBits = Math.Min(4, capacityBits - buffer.LengthInBits);
        if (terminatorBits > 0)
        {
            buffer.AppendBits(0, terminatorBits);
        }

        // Pad to byte boundary
        var remainder = buffer.LengthInBits % 8;
        if (remainder != 0)
        {
            buffer.AppendBits(0, 8 - remainder);
        }

        // Pad with alternating 0xEC, 0x11 to fill data capacity
        var padBytes = new[] { 0xEC, 0x11 };
        var padIndex = 0;
        while (buffer.LengthInBits < capacityBits)
        {
            buffer.AppendBits(padBytes[padIndex], 8);
            padIndex ^= 1;
        }

        return buffer.ToByteArray();
    }

    private static int ModeIndicator(QrEncodingMode mode) =>
        mode switch
        {
            QrEncodingMode.Numeric => 0b0001,
            QrEncodingMode.Alphanumeric => 0b0010,
            QrEncodingMode.Byte => 0b0100,
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported encoding mode."),
        };

    private static void EncodeNumeric(QrBitBuffer buffer, ReadOnlySpan<byte> data)
    {
        var i = 0;
        while (i + 2 < data.Length)
        {
            var value = (data[i] - 0x30) * 100 + (data[i + 1] - 0x30) * 10 + (data[i + 2] - 0x30);
            buffer.AppendBits(value, 10);
            i += 3;
        }

        if (data.Length - i == 2)
        {
            var value = (data[i] - 0x30) * 10 + (data[i + 1] - 0x30);
            buffer.AppendBits(value, 7);
        }
        else if (data.Length - i == 1)
        {
            buffer.AppendBits(data[i] - 0x30, 4);
        }
    }

    private static void EncodeAlphanumeric(QrBitBuffer buffer, ReadOnlySpan<byte> data)
    {
        var i = 0;
        while (i + 1 < data.Length)
        {
            var value = AlphanumericValues[data[i]] * 45 + AlphanumericValues[data[i + 1]];
            buffer.AppendBits(value, 11);
            i += 2;
        }

        if (i < data.Length)
        {
            buffer.AppendBits(AlphanumericValues[data[i]], 6);
        }
    }

    private static void EncodeByte(QrBitBuffer buffer, ReadOnlySpan<byte> data)
    {
        foreach (var b in data)
        {
            buffer.AppendBits(b, 8);
        }
    }

    private static int[] BuildAlphanumericTable()
    {
        var table = new int[128];

        // Initialize to -1 to catch invalid characters during development
        Array.Fill(table, -1);

        // 0-9 -> values 0-9
        for (var c = '0'; c <= '9'; c++)
        {
            table[c] = c - '0';
        }

        // A-Z -> values 10-35
        for (var c = 'A'; c <= 'Z'; c++)
        {
            table[c] = c - 'A' + 10;
        }

        // Special characters
        table[' '] = 36;
        table['$'] = 37;
        table['%'] = 38;
        table['*'] = 39;
        table['+'] = 40;
        table['-'] = 41;
        table['.'] = 42;
        table['/'] = 43;
        table[':'] = 44;

        return table;
    }
}
