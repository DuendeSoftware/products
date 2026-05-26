// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Authentication.QrCodes.Internal;

#pragma warning disable CA1814 // Multidimensional arrays are the natural representation for a QR matrix
internal static class QrDataPlacer
{
    internal static void PlaceDataBits(bool[,] modules, bool[,] isFunction, byte[] codewords, int remainderBits)
    {
        var size = modules.GetLength(0);
        var totalBits = codewords.Length * 8 + remainderBits;
        var bitIndex = 0;
        var goingUp = true;
        var rightCol = size - 1;

        while (rightCol >= 1)
        {
            // Skip the vertical timing pattern column
            if (rightCol == 6)
            {
                rightCol--;
            }

            for (var rowStep = 0; rowStep < size; rowStep++)
            {
                var row = goingUp ? size - 1 - rowStep : rowStep;

                for (var colOffset = 0; colOffset <= 1; colOffset++)
                {
                    var col = rightCol - colOffset;

                    if (isFunction[row, col])
                    {
                        continue;
                    }

                    if (bitIndex < totalBits)
                    {
                        var bit = false;
                        if (bitIndex < codewords.Length * 8)
                        {
                            var byteIndex = bitIndex / 8;
                            var bitPosition = 7 - (bitIndex % 8);
                            bit = ((codewords[byteIndex] >> bitPosition) & 1) == 1;
                        }
                        // else: remainder bits are 0 (bit stays false)

                        modules[row, col] = bit;
                        bitIndex++;
                    }
                }
            }

            goingUp = !goingUp;
            rightCol -= 2;
        }
    }
}
#pragma warning restore CA1814
