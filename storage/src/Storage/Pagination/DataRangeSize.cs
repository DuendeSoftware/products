// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.Pagination;

/// <summary>
/// The number of items per page or batch. Must be between 1 and <see cref="MaxValue"/> (1000).
/// </summary>
[ValueOf<int>]
public partial record DataRangeSize
{
    /// <summary>The default page size (25).</summary>
    public static readonly DataRangeSize Default = 25;

    /// <summary>The maximum allowed page size.</summary>
    public const int MaxValue = 1000;

    internal static bool TryValidate(int value, out IReadOnlyList<string>? errors)
    {
        errors = null;
        if (value < 1)
        {
            errors = ["Page size must be at least 1."];
            return false;
        }

        if (value > MaxValue)
        {
            errors = [$"Page size must not exceed {MaxValue}."];
            return false;
        }

        return true;
    }
}
