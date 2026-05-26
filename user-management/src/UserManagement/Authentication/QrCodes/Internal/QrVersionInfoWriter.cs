// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Authentication.QrCodes.Internal;

#pragma warning disable CA1814 // Multidimensional arrays are the natural representation for a QR matrix
internal static class QrVersionInfoWriter
{
    private const int GeneratorPolynomial = 0x1F25;

    internal static void WriteVersionInfo(bool[,] modules, bool[,] isFunction, int version)
    {
        if (version < 7)
        {
            return;
        }

        var size = modules.GetLength(0);
        var versionBits = EncodeVersionBits(version);

        for (var i = 0; i < 18; i++)
        {
            var bit = ((versionBits >> i) & 1) == 1;

            // Bottom-left
            var blRow = size - 11 + (i % 3);
            var blCol = i / 3;
            modules[blRow, blCol] = bit;

            // Top-right
            var trRow = i / 3;
            var trCol = size - 11 + (i % 3);
            modules[trRow, trCol] = bit;
        }
    }

    internal static int EncodeVersionBits(int version)
    {
        // BCH(18,6): 6-bit version number, 12-bit ECC
        var encoded = version << 12;
        var remainder = encoded;

        for (var i = 5; i >= 0; i--)
        {
            if ((remainder & (1 << (i + 12))) != 0)
            {
                remainder ^= GeneratorPolynomial << i;
            }
        }

        return (version << 12) | remainder;
    }
}
#pragma warning restore CA1814
