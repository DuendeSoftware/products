// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.Pagination;

/// <summary>
/// A starting position for pagination. Represents a 0-based row offset or a 1-based page number,
/// depending on context.
/// </summary>
[ValueOf<int>]
public partial record PageNumber
{
    internal static bool TryValidate(long value, out IReadOnlyList<string>? errors)
    {
        errors = null;
        if (value < 1)
        {
            errors = ["PageNumber must be at least 1."];
            return false;
        }

        return true;
    }
}
