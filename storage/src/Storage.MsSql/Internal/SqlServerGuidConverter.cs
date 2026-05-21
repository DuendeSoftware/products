// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.MsSql.Internal;

/// <summary>
/// Converts between standard (RFC 9562) UUIDv7 byte order and a SQL Server–optimized
/// byte layout that preserves chronological sort order in <c>UNIQUEIDENTIFIER</c> columns.
/// </summary>
/// <remarks>
/// <para>
/// SQL Server sorts <c>UNIQUEIDENTIFIER</c> values using the byte-comparison semantics
/// implemented in <see cref="System.Data.SqlTypes.SqlGuid"/>: the last six bytes (10-15)
/// are compared first (left-to-right), then bytes 8-9, 7, 6, 3, 2, 1, 0, 5, 4.
/// UUIDv7 places the 48-bit timestamp in bytes 0-5, which SQL Server compares last.
/// This means UUIDv7 values do not sort chronologically in SQL Server indexes.
/// </para>
/// <para>
/// This converter swaps the byte layout so that:
/// <list type="bullet">
///   <item>The 48-bit timestamp (originally bytes 0-5) is placed into bytes 10-15 (sorted first).</item>
///   <item>The version + rand_a (originally bytes 6-7) goes into bytes 8-9 (sorted second).</item>
///   <item>The variant + rand_b high (originally bytes 8-9) goes into bytes 6-7 (sorted third).</item>
///   <item>The rand_b low (originally bytes 10-15) goes into bytes 0-5 (sorted last).</item>
/// </list>
/// </para>
/// <para>
/// Since SQL Server evaluates bytes 10-15 first, placing the timestamp there ensures
/// that chronological order is preserved in clustered indexes and sort operations.
/// The conversion is its own inverse — applying it twice returns the original value.
/// </para>
/// </remarks>
internal static class SqlServerGuidConverter
{
    /// <summary>
    /// Converts a standard UUIDv7 to the SQL Server–optimized byte layout.
    /// </summary>
    internal static Guid ToSqlServer(Guid uuidV7)
    {
        Span<byte> bytes = stackalloc byte[16];
        _ = uuidV7.TryWriteBytes(bytes, bigEndian: true, out _);

        Span<byte> result = stackalloc byte[16];

        // Timestamp (bytes 0-5) → SQL Server high-priority position (bytes 10-15)
        bytes[..6].CopyTo(result[10..]);

        // Version + rand_a (bytes 6-7) → bytes 8-9
        bytes[6..8].CopyTo(result[8..]);

        // Variant + rand_b high (bytes 8-9) → bytes 6-7
        bytes[8..10].CopyTo(result[6..]);

        // Rand_b low (bytes 10-15) → bytes 0-5
        bytes[10..].CopyTo(result[..6]);

        return new Guid(result, bigEndian: true);
    }

    /// <summary>
    /// Converts a SQL Server–optimized GUID back to the standard UUIDv7 byte layout.
    /// The swap is its own inverse — the same byte permutation reverses itself.
    /// </summary>
    internal static Guid ToUuidV7(Guid sqlServerGuid) => ToSqlServer(sqlServerGuid);
}
