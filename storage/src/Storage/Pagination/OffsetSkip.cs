// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.Pagination;

/// <summary>
/// A validated 0-based row offset for offset-based pagination.
/// </summary>
[ValueOf<long>]
public partial record OffsetSkip
{
    internal static bool TryValidate(long value, out IReadOnlyList<string>? errors)
    {
        errors = null;
        if (value < 0)
        {
            errors = ["Start position must be at least 0."];
            return false;
        }

        return true;
    }
}
