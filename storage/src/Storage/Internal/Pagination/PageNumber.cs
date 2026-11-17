// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.Internal.Pagination;

/// <summary>
/// A validated 1-based page number for page-number-based pagination.
/// </summary>
/// <remarks>
/// This type is for usage by Duende Software products, is not supported for end user consumption, and not subject to semantic versioning rules.
/// </remarks>
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
