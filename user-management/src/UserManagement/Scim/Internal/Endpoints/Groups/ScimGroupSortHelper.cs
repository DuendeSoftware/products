// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Querying;
using Duende.UserManagement.Membership;

namespace Duende.UserManagement.Scim.Internal.Endpoints.Groups;

/// <summary>
/// Shared sort-field mapping for SCIM Group list and search endpoints.
/// </summary>
internal static class ScimGroupSortHelper
{
    /// <summary>
    /// Maps a SCIM <c>sortBy</c> attribute name to a <see cref="GroupSortField"/>.
    /// Unsupported sort attributes are silently ignored per RFC 7644 §3.4.2.3.
    /// </summary>
    internal static SortBy.SortByField<GroupSortField>? MapSortBy(
        string? sortBy, SortDirection direction)
    {
        if (string.IsNullOrWhiteSpace(sortBy))
        {
            return null;
        }

        if (string.Equals(sortBy, ScimConstants.Attributes.DisplayName, StringComparison.OrdinalIgnoreCase))
        {
            return SortBy.Field(GroupSortField.Name, direction);
        }

        // Unsupported sort attributes are silently ignored per RFC 7644 §3.4.2.3
        return null;
    }
}
