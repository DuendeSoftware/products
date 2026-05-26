// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal;
using Duende.Storage.Internal.Querying;
using Duende.Storage.Internal.Querying.Fields;

namespace Duende.UserManagement.Membership.Internal.Storage;

/// <summary>
/// Resolves query attribute paths to storage Field types for Group entities.
/// Maps well-known group attribute names (e.g., "displayName") to their
/// corresponding search fields used in <see cref="GroupRepository"/>.
/// </summary>
internal sealed class GroupAttributeTypeResolver : IQueryAttributeTypeResolver
{
    private static readonly Dictionary<string, Field> Fields = new(StringComparer.OrdinalIgnoreCase)
    {
        ["displayName"] = new StringField("Name"),
    };

    /// <inheritdoc />
    public Field ResolveField(string attributePath)
    {
        // Handle system timestamp fields available on all entity types
        if (string.Equals(attributePath, SystemFields.CreatedAttributeName, StringComparison.OrdinalIgnoreCase))
        {
            return SystemFields.CreatedAtField;
        }

        if (string.Equals(attributePath, SystemFields.LastUpdatedAttributeName, StringComparison.OrdinalIgnoreCase))
        {
            return SystemFields.LastUpdatedAtField;
        }

        if (Fields.TryGetValue(attributePath, out var field))
        {
            return field;
        }

        var supported = Fields.Keys.Concat([SystemFields.CreatedAttributeName, SystemFields.LastUpdatedAttributeName]);
        throw new NotSupportedException(
            $"Unknown group attribute: {attributePath}. Supported attributes: {string.Join(", ", supported)}");
    }
}
