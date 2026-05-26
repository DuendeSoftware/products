// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Authentication.QrCodes.Internal;

#pragma warning disable CA1814 // Multidimensional arrays are the natural representation for a QR matrix
internal static class QrMasker
{
    internal static (bool[,] MaskedModules, int MaskIndex) ApplyBestMask(bool[,] modules, bool[,] isFunction)
    {
        var size = modules.GetLength(0);
        var bestScore = int.MaxValue;
        var bestMask = 0;
        bool[,]? bestModules = null;

        for (var mask = 0; mask < 8; mask++)
        {
            var candidate = (bool[,])modules.Clone();
            ApplyMask(candidate, isFunction, mask, size);

            var score = ComputePenalty(candidate);
            if (score < bestScore)
            {
                bestScore = score;
                bestMask = mask;
                bestModules = candidate;
            }
        }

        return (bestModules!, bestMask);
    }

    internal static int ComputePenalty(bool[,] modules)
    {
        var size = modules.GetLength(0);
        return PenaltyN1(modules, size)
             + PenaltyN2(modules, size)
             + PenaltyN3(modules, size)
             + PenaltyN4(modules, size);
    }

    private static void ApplyMask(bool[,] modules, bool[,] isFunction, int mask, int size)
    {
        for (var row = 0; row < size; row++)
        {
            for (var col = 0; col < size; col++)
            {
                if (isFunction[row, col])
                {
                    continue;
                }

                if (MaskCondition(mask, row, col))
                {
                    modules[row, col] = !modules[row, col];
                }
            }
        }
    }

    private static bool MaskCondition(int mask, int row, int col) =>
        mask switch
        {
            0 => (row + col) % 2 == 0,
            1 => row % 2 == 0,
            2 => col % 3 == 0,
            3 => (row + col) % 3 == 0,
            4 => (row / 2 + col / 3) % 2 == 0,
            5 => (row * col) % 2 + (row * col) % 3 == 0,
            6 => ((row * col) % 2 + (row * col) % 3) % 2 == 0,
            7 => ((row + col) % 2 + (row * col) % 3) % 2 == 0,
            _ => throw new ArgumentOutOfRangeException(nameof(mask)),
        };

    private static int PenaltyN1(bool[,] modules, int size)
    {
        var penalty = 0;

        // Rows
        for (var row = 0; row < size; row++)
        {
            var runLength = 1;
            for (var col = 1; col < size; col++)
            {
                if (modules[row, col] == modules[row, col - 1])
                {
                    runLength++;
                }
                else
                {
                    if (runLength >= 5)
                    {
                        penalty += 3 + (runLength - 5);
                    }

                    runLength = 1;
                }
            }

            if (runLength >= 5)
            {
                penalty += 3 + (runLength - 5);
            }
        }

        // Columns
        for (var col = 0; col < size; col++)
        {
            var runLength = 1;
            for (var row = 1; row < size; row++)
            {
                if (modules[row, col] == modules[row - 1, col])
                {
                    runLength++;
                }
                else
                {
                    if (runLength >= 5)
                    {
                        penalty += 3 + (runLength - 5);
                    }

                    runLength = 1;
                }
            }

            if (runLength >= 5)
            {
                penalty += 3 + (runLength - 5);
            }
        }

        return penalty;
    }

    private static int PenaltyN2(bool[,] modules, int size)
    {
        var penalty = 0;

        for (var row = 0; row < size - 1; row++)
        {
            for (var col = 0; col < size - 1; col++)
            {
                var color = modules[row, col];
                if (color == modules[row, col + 1] &&
                    color == modules[row + 1, col] &&
                    color == modules[row + 1, col + 1])
                {
                    penalty += 3;
                }
            }
        }

        return penalty;
    }

    private static int PenaltyN3(bool[,] modules, int size)
    {
        var penalty = 0;

        ReadOnlySpan<bool> pattern1 = [true, false, true, true, true, false, true, false, false, false, false];
        ReadOnlySpan<bool> pattern2 = [false, false, false, false, true, false, true, true, true, false, true];

        // Rows
        for (var row = 0; row < size; row++)
        {
            for (var col = 0; col <= size - 11; col++)
            {
                if (MatchesPattern(modules, row, col, pattern1, horizontal: true) ||
                    MatchesPattern(modules, row, col, pattern2, horizontal: true))
                {
                    penalty += 40;
                }
            }
        }

        // Columns
        for (var col = 0; col < size; col++)
        {
            for (var row = 0; row <= size - 11; row++)
            {
                if (MatchesPattern(modules, row, col, pattern1, horizontal: false) ||
                    MatchesPattern(modules, row, col, pattern2, horizontal: false))
                {
                    penalty += 40;
                }
            }
        }

        return penalty;
    }

    private static bool MatchesPattern(bool[,] modules, int row, int col, ReadOnlySpan<bool> pattern, bool horizontal)
    {
        for (var i = 0; i < pattern.Length; i++)
        {
            var value = horizontal ? modules[row, col + i] : modules[row + i, col];
            if (value != pattern[i])
            {
                return false;
            }
        }

        return true;
    }

    private static int PenaltyN4(bool[,] modules, int size)
    {
        var darkCount = 0;
        var totalModules = size * size;

        for (var row = 0; row < size; row++)
        {
            for (var col = 0; col < size; col++)
            {
                if (modules[row, col])
                {
                    darkCount++;
                }
            }
        }

        var percentage = darkCount * 100 / totalModules;
        var prevMultiple = percentage / 5 * 5;
        var nextMultiple = prevMultiple + 5;

        return Math.Min(Math.Abs(prevMultiple - 50), Math.Abs(nextMultiple - 50)) / 5 * 10;
    }
}
#pragma warning restore CA1814
