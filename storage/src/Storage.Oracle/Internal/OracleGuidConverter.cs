// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.Oracle.Internal;

/// <summary>
/// Converts between <see cref="Guid"/> values and the 16-byte <c>RAW(16)</c> representation
/// stored in Oracle.
/// </summary>
/// <remarks>
/// <para>
/// Oracle compares <c>RAW</c> columns using plain big-endian byte comparison. UUIDv7 places its
/// 48-bit timestamp in the leading bytes (RFC 9562 / big-endian order), so storing the GUID in
/// big-endian byte order makes Oracle's <c>RAW</c> index sort chronologically — no byte swizzling
/// (as required for SQL Server's <c>UNIQUEIDENTIFIER</c>) is needed.
/// </para>
/// <para>
/// ODP.NET's default <see cref="Guid"/> &lt;-&gt; <c>RAW(16)</c> mapping uses the mixed-endian
/// layout produced by <see cref="Guid.ToByteArray()"/>, which would scramble the timestamp bytes
/// and break chronological ordering. This converter forces the canonical big-endian layout on the
/// way in and reconstructs the GUID from the same layout on the way out.
/// </para>
/// </remarks>
internal static class OracleGuidConverter
{
    /// <summary>
    /// Converts a <see cref="Guid"/> to its canonical big-endian 16-byte representation for
    /// storage in an Oracle <c>RAW(16)</c> column.
    /// </summary>
    internal static byte[] ToRaw(Guid value)
    {
        var bytes = new byte[16];
        _ = value.TryWriteBytes(bytes, bigEndian: true, out _);
        return bytes;
    }

    /// <summary>
    /// Reconstructs a <see cref="Guid"/> from the canonical big-endian 16-byte representation
    /// read from an Oracle <c>RAW(16)</c> column.
    /// </summary>
    internal static Guid FromRaw(byte[] raw) => new(raw, bigEndian: true);
}
