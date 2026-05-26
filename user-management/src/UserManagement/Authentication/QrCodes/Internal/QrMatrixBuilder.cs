// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Authentication.QrCodes.Internal;

#pragma warning disable CA1814 // Multidimensional arrays are the natural representation for a QR matrix
internal static class QrMatrixBuilder
{
    internal static (bool[,] Modules, bool[,] IsFunction) BuildFunctionPatterns(int version, QrEccLevel ecc)
    {
        var size = 17 + 4 * version;
        var modules = new bool[size, size];
        var isFunction = new bool[size, size];

        PlaceFinderPatterns(modules, isFunction, size);
        PlaceSeparators(modules, isFunction, size);
        PlaceTimingPatterns(modules, isFunction, size);
        PlaceAlignmentPatterns(modules, isFunction, version, size);
        PlaceDarkModule(modules, isFunction, version);
        ReserveFormatInfoAreas(isFunction, size);
        ReserveVersionInfoAreas(isFunction, version, size);

        return (modules, isFunction);
    }

    private static void PlaceFinderPatterns(bool[,] modules, bool[,] isFunction, int size)
    {
        PlaceFinderPattern(modules, isFunction, 0, 0);
        PlaceFinderPattern(modules, isFunction, 0, size - 7);
        PlaceFinderPattern(modules, isFunction, size - 7, 0);
    }

    private static void PlaceFinderPattern(bool[,] modules, bool[,] isFunction, int startRow, int startCol)
    {
        for (var row = 0; row < 7; row++)
        {
            for (var col = 0; col < 7; col++)
            {
                var isDark = row == 0 || row == 6 || col == 0 || col == 6 ||
                             (row >= 2 && row <= 4 && col >= 2 && col <= 4);

                modules[startRow + row, startCol + col] = isDark;
                isFunction[startRow + row, startCol + col] = true;
            }
        }
    }

    private static void PlaceSeparators(bool[,] modules, bool[,] isFunction, int size)
    {
        // Top-left finder separator
        for (var i = 0; i <= 7; i++)
        {
            // Horizontal: row 7, cols 0-7
            modules[7, i] = false;
            isFunction[7, i] = true;
            // Vertical: col 7, rows 0-7
            modules[i, 7] = false;
            isFunction[i, 7] = true;
        }

        // Top-right finder separator
        for (var i = 0; i <= 7; i++)
        {
            // Horizontal: row 7, cols (size-8) to (size-1)
            modules[7, size - 8 + i] = false;
            isFunction[7, size - 8 + i] = true;
            // Vertical: col (size-8), rows 0-7
            modules[i, size - 8] = false;
            isFunction[i, size - 8] = true;
        }

        // Bottom-left finder separator
        for (var i = 0; i <= 7; i++)
        {
            // Horizontal: row (size-8), cols 0-7
            modules[size - 8, i] = false;
            isFunction[size - 8, i] = true;
            // Vertical: col 7, rows (size-8) to (size-1)
            modules[size - 8 + i, 7] = false;
            isFunction[size - 8 + i, 7] = true;
        }
    }

    private static void PlaceTimingPatterns(bool[,] modules, bool[,] isFunction, int size)
    {
        for (var i = 8; i <= size - 9; i++)
        {
            var isDark = (i - 8) % 2 == 0;

            // Horizontal timing: row 6
            modules[6, i] = isDark;
            isFunction[6, i] = true;

            // Vertical timing: col 6
            modules[i, 6] = isDark;
            isFunction[i, 6] = true;
        }
    }

    private static void PlaceAlignmentPatterns(bool[,] modules, bool[,] isFunction, int version, int size)
    {
        if (version < 2)
        {
            return;
        }

        var info = QrVersionTables.Get(version, QrEccLevel.L); // alignment positions are same for all ECC levels
        var positions = info.AlignmentPatternPositions;

        foreach (var row in positions)
        {
            foreach (var col in positions)
            {
                // Skip if overlapping with finder pattern regions
                if (row <= 8 && col <= 8)
                {
                    continue; // top-left
                }

                if (row <= 8 && col >= size - 8)
                {
                    continue; // top-right
                }

                if (row >= size - 8 && col <= 8)
                {
                    continue; // bottom-left
                }

                PlaceAlignmentPattern(modules, isFunction, row, col);
            }
        }
    }

    private static void PlaceAlignmentPattern(bool[,] modules, bool[,] isFunction, int centerRow, int centerCol)
    {
        for (var dr = -2; dr <= 2; dr++)
        {
            for (var dc = -2; dc <= 2; dc++)
            {
                var isDark = dr == -2 || dr == 2 || dc == -2 || dc == 2 || (dr == 0 && dc == 0);
                modules[centerRow + dr, centerCol + dc] = isDark;
                isFunction[centerRow + dr, centerCol + dc] = true;
            }
        }
    }

    private static void PlaceDarkModule(bool[,] modules, bool[,] isFunction, int version)
    {
        var row = 4 * version + 9;
        modules[row, 8] = true;
        isFunction[row, 8] = true;
    }

    private static void ReserveFormatInfoAreas(bool[,] isFunction, int size)
    {
        // Around top-left finder: row 8, cols 0-8 and col 8, rows 0-8
        for (var i = 0; i <= 8; i++)
        {
            isFunction[8, i] = true;
            isFunction[i, 8] = true;
        }

        // Bottom-left: col 8, rows (size-7) to (size-1)
        for (var row = size - 7; row <= size - 1; row++)
        {
            isFunction[row, 8] = true;
        }

        // Top-right: row 8, cols (size-8) to (size-1)
        for (var col = size - 8; col <= size - 1; col++)
        {
            isFunction[8, col] = true;
        }
    }

    private static void ReserveVersionInfoAreas(bool[,] isFunction, int version, int size)
    {
        if (version < 7)
        {
            return;
        }

        // Bottom-left area: 3 rows x 6 cols at rows (size-11) to (size-9), cols 0 to 5
        for (var row = size - 11; row <= size - 9; row++)
        {
            for (var col = 0; col <= 5; col++)
            {
                isFunction[row, col] = true;
            }
        }

        // Top-right area: 6 rows x 3 cols at rows 0 to 5, cols (size-11) to (size-9)
        for (var row = 0; row <= 5; row++)
        {
            for (var col = size - 11; col <= size - 9; col++)
            {
                isFunction[row, col] = true;
            }
        }
    }
}
#pragma warning restore CA1814
