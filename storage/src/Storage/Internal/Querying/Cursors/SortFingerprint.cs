// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Cryptography;
using System.Text;
using Duende.Storage.Internal.Querying.Fields;
using Duende.Storage.Internal.Querying.Sorting;

namespace Duende.Storage.Internal.Querying.Cursors;

/// <summary>
/// Computes the 8-byte sort fingerprint stored in a cursor token, identifying the entity type, field,
/// field kind and direction a token was issued for.
/// </summary>
/// <remarks>
/// This type is for usage by Duende Software products, is not supported for end user consumption, and not subject to semantic versioning rules.
/// </remarks>
internal static class SortFingerprint
{
    /// <summary>
    /// The number of bytes a fingerprint occupies.
    /// </summary>
    public const int Length = 8;

    /// <summary>
    /// Computes the fingerprint for the given entity type and sort.
    /// </summary>
    /// <param name="entityType">The entity type the query runs against.</param>
    /// <param name="sort">The sort parameter, or <see cref="SortParameter.Empty"/> for an unsorted query.</param>
    /// <returns>The first 8 bytes of <c>SHA256(UTF8(formula))</c>.</returns>
    public static byte[] Compute(EntityType entityType, SortParameter sort)
    {
        var formula = sort.IsEmpty
            ? $"o1|{entityType.Id}|default"
            : $"o1|{entityType.Id}|{sort.Field.Path.ToUpperInvariant()}|{MapValueKind(sort.Field):D}|{sort.Direction:D}";

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(formula));
        return hash[..Length];
    }

    /// <summary>
    /// Maps a query field to the <see cref="CursorValueKind"/> used in the fingerprint formula and the
    /// token's value kind, the same way providers choose a sort column for the field.
    /// </summary>
    public static CursorValueKind MapValueKind(Field field) => field.Type switch
    {
        FieldType.String => CursorValueKind.String,
        FieldType.Number => CursorValueKind.Number,
        FieldType.DateTime => CursorValueKind.DateTime,
        FieldType.Boolean => CursorValueKind.Boolean,
        FieldType.Guid => CursorValueKind.Guid,
        _ => throw new InvalidOperationException($"Unsupported field type for sorting: {field.Type}")
    };
}
