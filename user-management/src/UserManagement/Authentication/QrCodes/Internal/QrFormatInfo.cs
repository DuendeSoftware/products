// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Authentication.QrCodes.Internal;

#pragma warning disable CA1814 // Multidimensional arrays are the natural representation for a QR matrix
internal static class QrFormatInfo
{
    private const int GeneratorPolynomial = 0x537;
    private const int FormatMask = 0x5412;

    internal static void WriteFormatInfo(bool[,] modules, bool[,] isFunction, QrEccLevel ecc, int maskIndex)
    {
        var size = modules.GetLength(0);
        var formatBits = EncodeFormatBits(ecc, maskIndex);

        // Copy 1: top-left
        int[] copy1Rows = [8, 8, 8, 8, 8, 8, 8, 8, 7, 5, 4, 3, 2, 1, 0];
        int[] copy1Cols = [0, 1, 2, 3, 4, 5, 7, 8, 8, 8, 8, 8, 8, 8, 8];

        for (var i = 0; i < 15; i++)
        {
            var bit = ((formatBits >> (14 - i)) & 1) == 1;
            modules[copy1Rows[i], copy1Cols[i]] = bit;
        }

        // Copy 2: bottom-left + top-right
        for (var i = 0; i < 15; i++)
        {
            var bit = ((formatBits >> (14 - i)) & 1) == 1;

            int row, col;
            if (i < 7)
            {
                // Bottom-left: bits 0-6
                row = size - 1 - i;
                col = 8;
            }
            else
            {
                // Top-right: bits 7-14
                row = 8;
                col = size - 8 + (i - 7);
            }

            modules[row, col] = bit;
        }
    }

    internal static int EncodeFormatBits(QrEccLevel ecc, int maskIndex)
    {
        var eccBits = EccToBits(ecc);
        var data = (eccBits << 3) | maskIndex;

        // BCH(15,5): shift data left by 10 and compute remainder
        var encoded = data << 10;
        var remainder = encoded;

        for (var i = 4; i >= 0; i--)
        {
            if ((remainder & (1 << (i + 10))) != 0)
            {
                remainder ^= GeneratorPolynomial << i;
            }
        }

        var result = (data << 10) | remainder;
        return result ^ FormatMask;
    }

    private static int EccToBits(QrEccLevel ecc) =>
        ecc switch
        {
            QrEccLevel.L => 0b01,
            QrEccLevel.M => 0b00,
            QrEccLevel.Q => 0b11,
            QrEccLevel.H => 0b10,
            _ => throw new ArgumentOutOfRangeException(nameof(ecc)),
        };
}
#pragma warning restore CA1814
