// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Buffers.Binary;
using System.Buffers.Text;
using System.Text;
using Duende.Storage.Internal.Querying.SearchFields;
using Duende.Storage.Internal.Querying.Sorting;
using Duende.Storage.Internal.Pagination;

namespace Duende.Storage.Internal.Querying.Cursors;

/// <summary>
/// A strict, versioned binary cursor token: direction, position (start or a specific key) and a sort
/// fingerprint, encoded as Base64Url. See the byte layout in the design notes below.
/// </summary>
/// <remarks>
/// This type is for usage by Duende Software products, is not supported for end user consumption, and not subject to semantic versioning rules.
///
/// Byte layout (little-endian multi-byte integers):
/// <list type="table">
/// <item><term>0 (1 byte)</term><description>Format version. Only <c>1</c> is understood.</description></item>
/// <item><term>1 (1 byte)</term><description>
/// Flags: bit0 = direction (0 forward, 1 backward); bit1 = position kind (0 start, 1 key);
/// bits 2-4 = <see cref="CursorValueKind"/>; bits 5-7 reserved, must be 0.
/// </description></item>
/// <item><term>2 (8 bytes)</term><description>Sort fingerprint, see <see cref="SortFingerprint"/>.</description></item>
/// <item><term>10 (16 bytes)</term><description>Key only: entity id, <see cref="Guid.TryWriteBytes(Span{byte},bool,out int)"/> with <c>bigEndian: false</c>.</description></item>
/// <item><term>26 (variable)</term><description>Key only: the sort value, encoded per <see cref="CursorValueKind"/>. For
/// <see cref="CursorValueKind.String"/> this is a 2-byte little-endian UTF-8 byte length, followed by exactly that
/// many UTF-8 bytes; every other kind has a fixed length, so the token's total length is always exactly
/// determined by its header.</description></item>
/// </list>
/// A value kind that doesn't match the field's kind is rejected as <see cref="InvalidContinuationTokenReason.SortMismatch"/>,
/// even if the fingerprint happens to match, as a defensive check (the fingerprint already covers the field kind).
/// A token that was changed by hand isn't always detected as invalid: because most fields have no further
/// validation beyond their fixed length and value kind, a hand-edited token can still decode successfully. It can
/// still only move the position within the data the caller's own filter and entity type already allow it to see.
/// </remarks>
internal sealed record CursorToken(CursorDirection Direction, CursorPosition Position, ReadOnlyMemory<byte> Fingerprint)
{
    private const byte FormatVersion = 1;
    private const int HeaderLength = 10; // version(1) + flags(1) + fingerprint(8)
    private const int KeyPrefixLength = HeaderLength + 16; // + entity id(16)

    // The maximum number of bytes a string sort value can occupy once UTF-8 encoded: each of the
    // SearchFieldValueLimits.MaxStringValueLength characters can be up to 4 bytes (a surrogate pair
    // encoding a single astral codepoint), plus the 2-byte length prefix written before the bytes.
    internal const int MaxStringValueByteLength = 2 + (SearchFieldValueLimits.MaxStringValueLength * 4);

    // The largest buffer Encode can produce: the key prefix plus the longest allowed string sort value.
    private const int MaxKeyBufferLength = KeyPrefixLength + MaxStringValueByteLength;

    // Base64Url chars needed to encode MaxKeyBufferLength bytes, computed from the constants above so
    // Decode's length cap always matches what Encode can actually produce.
    internal static readonly int MaxEncodedChars = Base64Url.GetEncodedLength(MaxKeyBufferLength);


    private const byte DirectionBit = 0b0000_0001;
    private const byte PositionKeyBit = 0b0000_0010;
    private const byte ValueKindShift = 2;
    private const byte ValueKindMask = 0b0001_1100;
    private const byte ReservedBitsMask = 0b1110_0000;

    /// <summary>
    /// Builds the token for the start of the traversal (before the first row forward, or after the last row backward).
    /// </summary>
    public static CursorToken ForStart(CursorDirection direction, EntityType entityType, SortParameter sort) =>
        new(direction, CursorPosition.Start, SortFingerprint.Compute(entityType, sort));

    /// <summary>
    /// Builds the token for an already-decoded position: used when assembling an empty page's
    /// Next/Previous tokens from the request's position instead of a fetched row.
    /// </summary>
    public static CursorToken ForPosition(CursorDirection direction, CursorPosition position, EntityType entityType, SortParameter sort) =>
        new(direction, position, SortFingerprint.Compute(entityType, sort));

    /// <summary>
    /// Builds the token for a specific row: its entity id and sort value.
    /// </summary>
    /// <param name="direction">The direction this token continues in.</param>
    /// <param name="entityType">The entity type the query ran against.</param>
    /// <param name="sort">The sort parameter the query ran with, or <see cref="SortParameter.Empty"/> when unsorted.</param>
    /// <param name="id">The row's entity id.</param>
    /// <param name="sortValue">
    /// The row's raw sort field value (<see cref="string"/>, <see cref="decimal"/>, <see cref="DateTime"/>,
    /// <see cref="DateTimeOffset"/>, <see cref="bool"/>, <see cref="Guid"/>, or null), or null when <paramref name="sort"/> is empty.
    /// </param>
    public static CursorToken ForPage(CursorDirection direction, EntityType entityType, SortParameter sort, Guid id, object? sortValue)
    {
        var fingerprint = SortFingerprint.Compute(entityType, sort);

        if (sort.IsEmpty)
        {
            return new CursorToken(direction, CursorPosition.ForUnsortedKey(id), fingerprint);
        }

        var (kind, value) = NormalizeValue(sortValue);
        return new CursorToken(direction, CursorPosition.ForKey(id, kind, value), fingerprint);
    }

    private static (CursorValueKind Kind, object? Value) NormalizeValue(object? sortValue) => sortValue switch
    {
        null => (CursorValueKind.Null, null),
        string s => (CursorValueKind.String, s),
        decimal d => (CursorValueKind.Number, d),
        DateTime dt => (CursorValueKind.DateTime, new DateTimeOffset(dt, TimeSpan.Zero).ToUniversalTime()),
        DateTimeOffset dto => (CursorValueKind.DateTime, dto.ToUniversalTime()),
        bool b => (CursorValueKind.Boolean, b),
        Guid g => (CursorValueKind.Guid, g),
        _ => throw new InvalidOperationException($"Unsupported sort value type: {sortValue.GetType().Name}")
    };

    /// <summary>
    /// Encodes this token as an opaque, Base64Url continuation token.
    /// </summary>
    public ContinuationToken Encode()
    {
        if (Position.Kind == CursorPositionKind.Start)
        {
            var startBuffer = new byte[HeaderLength];
            WriteHeader(startBuffer, CursorValueKind.None);
            return ContinuationToken.Create(Base64Url.EncodeToString(startBuffer));
        }

        if (Position.ValueKind == CursorValueKind.String && Position.Value is string stringValue)
        {
            if (stringValue.Length > SearchFieldValueLimits.MaxStringValueLength)
            {
                throw new InvalidOperationException(
                    $"Cannot encode a continuation token: the string sort value is {stringValue.Length} characters long, " +
                    $"which exceeds the {SearchFieldValueLimits.MaxStringValueLength}-character limit for indexed string values. " +
                    "This value must have been stored before the limit existed; it cannot be used as a cursor position.");
            }

            if (stringValue.Contains('\0', StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Cannot encode a continuation token: the string sort value contains a null character (U+0000), " +
                    "which indexed string values may not contain. This value must have been stored before that rule " +
                    "existed; it cannot be used as a cursor position.");
            }
        }

        var valueLength = ValueByteLength(Position.ValueKind, Position.Value);

        var keyBuffer = new byte[KeyPrefixLength + valueLength];
        WriteHeader(keyBuffer, Position.ValueKind);
        _ = Position.Id.TryWriteBytes(keyBuffer.AsSpan(HeaderLength, 16), bigEndian: false, out _);
        WriteValue(keyBuffer.AsSpan(KeyPrefixLength), Position.ValueKind, Position.Value);
        return ContinuationToken.Create(Base64Url.EncodeToString(keyBuffer));
    }

    private void WriteHeader(Span<byte> buffer, CursorValueKind valueKind)
    {
        buffer[0] = FormatVersion;
        byte flags = 0;
        if (Direction == CursorDirection.Backward)
        {
            flags |= DirectionBit;
        }

        if (Position.Kind == CursorPositionKind.Key)
        {
            flags |= PositionKeyBit;
        }

        flags |= (byte)((int)valueKind << ValueKindShift);
        buffer[1] = flags;
        Fingerprint.Span.CopyTo(buffer.Slice(2, SortFingerprint.Length));
    }

    private static int ValueByteLength(CursorValueKind kind, object? value) => kind switch
    {
        CursorValueKind.None or CursorValueKind.Null => 0,
        CursorValueKind.Number => 16,
        CursorValueKind.DateTime => 8,
        CursorValueKind.Boolean => 1,
        CursorValueKind.Guid => 16,
        CursorValueKind.String => 2 + Encoding.UTF8.GetByteCount((string)value!),
        _ => throw new InvalidOperationException($"Unsupported cursor value kind: {kind}")
    };

    private static void WriteValue(Span<byte> destination, CursorValueKind kind, object? value)
    {
        switch (kind)
        {
            case CursorValueKind.None:
            case CursorValueKind.Null:
                break;
            case CursorValueKind.Number:
                var bits = decimal.GetBits((decimal)value!);
                for (var i = 0; i < 4; i++)
                {
                    BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(i * 4, 4), bits[i]);
                }

                break;
            case CursorValueKind.DateTime:
                var instant = (DateTimeOffset)value!;
                BinaryPrimitives.WriteInt64LittleEndian(destination, instant.UtcDateTime.Ticks);
                break;
            case CursorValueKind.Boolean:
                destination[0] = (bool)value! ? (byte)1 : (byte)0;
                break;
            case CursorValueKind.Guid:
                _ = ((Guid)value!).TryWriteBytes(destination, bigEndian: false, out _);
                break;
            case CursorValueKind.String:
                var stringBytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetBytes((string)value!);
                BinaryPrimitives.WriteUInt16LittleEndian(destination, (ushort)stringBytes.Length);
                stringBytes.CopyTo(destination[2..]);
                break;
            default:
                throw new InvalidOperationException($"Unsupported cursor value kind: {kind}");
        }
    }

    /// <summary>
    /// Decodes and validates a continuation token for the given entity type and sort, in strict recognition order:
    /// the <see cref="ContinuationToken.Beginning"/> literal, a length cap, Base64Url decoding, the format version,
    /// the byte layout, and finally the sort fingerprint and value kind. Never returns null; an undecodable or
    /// mismatched token always throws <see cref="InvalidContinuationTokenException"/>.
    /// </summary>
    /// <param name="token">The continuation token to decode.</param>
    /// <param name="sort">The sort parameter the query is running with, or <see cref="SortParameter.Empty"/> when unsorted.</param>
    /// <param name="entityType">The entity type the query runs against.</param>
    /// <exception cref="InvalidContinuationTokenException">The token is malformed, uses an unsupported version, or doesn't match <paramref name="sort"/>/<paramref name="entityType"/>.</exception>
    public static CursorToken Decode(ContinuationToken token, SortParameter sort, EntityType entityType)
    {
        var raw = token.ToString();

        if (string.Equals(raw, ContinuationToken.Beginning, StringComparison.Ordinal))
        {
            return ForStart(CursorDirection.Forward, entityType, sort);
        }

        if (raw.Length > MaxEncodedChars)
        {
            throw Malformed("Continuation token exceeds the maximum supported length.");
        }

        var maxByteLength = Base64Url.GetMaxDecodedLength(raw.Length);
        var buffer = new byte[maxByteLength];
        int bytesWritten;
        try
        {
            if (!Base64Url.TryDecodeFromChars(raw, buffer, out bytesWritten))
            {
                throw Malformed("Continuation token is not valid Base64Url.");
            }
        }
        catch (FormatException ex)
        {
            throw Malformed("Continuation token is not valid Base64Url.", ex);
        }

        var bytes = buffer.AsSpan(0, bytesWritten);

        if (bytes.Length < HeaderLength)
        {
            throw Malformed("Continuation token is too short.");
        }

        if (bytes[0] != FormatVersion)
        {
            throw new InvalidContinuationTokenException(
                InvalidContinuationTokenReason.UnsupportedVersion,
                $"Continuation token format version {bytes[0]} is not supported.");
        }

        var flags = bytes[1];
        if ((flags & ReservedBitsMask) != 0)
        {
            throw Malformed("Continuation token has reserved flag bits set.");
        }

        var direction = (flags & DirectionBit) != 0 ? CursorDirection.Backward : CursorDirection.Forward;
        var isKey = (flags & PositionKeyBit) != 0;
        var rawValueKind = (flags & ValueKindMask) >> ValueKindShift;
        if (rawValueKind is < 0 or > 6)
        {
            throw Malformed("Continuation token has an unknown sort value kind.");
        }

        var valueKind = (CursorValueKind)rawValueKind;
        var fingerprint = bytes.Slice(2, SortFingerprint.Length).ToArray();

        CursorPosition position;
        if (!isKey)
        {
            if (valueKind != CursorValueKind.None || bytes.Length != HeaderLength)
            {
                throw Malformed("Continuation token for the start position has an unexpected layout.");
            }

            position = CursorPosition.Start;
        }
        else
        {
            if (bytes.Length < KeyPrefixLength)
            {
                throw Malformed("Continuation token is too short for a key position.");
            }

            var id = new Guid(bytes.Slice(HeaderLength, 16), bigEndian: false);
            var valueBytes = bytes[KeyPrefixLength..];
            var value = DecodeValue(valueKind, valueBytes);
            position = CursorPosition.ForKey(id, valueKind, value);
        }

        var expectedFingerprint = SortFingerprint.Compute(entityType, sort);
        if (!fingerprint.AsSpan().SequenceEqual(expectedFingerprint))
        {
            throw new InvalidContinuationTokenException(
                InvalidContinuationTokenReason.SortMismatch,
                "Continuation token was issued for a different entity type, sort field, field kind, or direction.");
        }

        ValidateValueKindMatchesSort(isKey, valueKind, sort);

        return new CursorToken(direction, position, expectedFingerprint);
    }

    private static void ValidateValueKindMatchesSort(bool isKey, CursorValueKind valueKind, SortParameter sort)
    {
        if (!isKey)
        {
            // Start carries no sort value and is valid for any sort; its layout was already validated above.
            return;
        }

        var expectedKind = sort.IsEmpty ? CursorValueKind.None : SortFingerprint.MapValueKind(sort.Field);
        if (valueKind == CursorValueKind.None)
        {
            // An unsorted key token must match an unsorted query.
            if (!sort.IsEmpty)
            {
                throw new InvalidContinuationTokenException(
                    InvalidContinuationTokenReason.SortMismatch,
                    "Continuation token carries no sort value, but the query is sorted.");
            }

            return;
        }

        if (sort.IsEmpty)
        {
            throw new InvalidContinuationTokenException(
                InvalidContinuationTokenReason.SortMismatch,
                "Continuation token carries a sort value, but the query is unsorted.");
        }

        if (valueKind != CursorValueKind.Null && valueKind != expectedKind)
        {
            throw new InvalidContinuationTokenException(
                InvalidContinuationTokenReason.SortMismatch,
                "Continuation token's sort value kind does not match the query's sort field.");
        }
    }

    private static object? DecodeValue(CursorValueKind kind, ReadOnlySpan<byte> bytes)
    {
        switch (kind)
        {
            case CursorValueKind.None:
            case CursorValueKind.Null:
                if (bytes.Length != 0)
                {
                    throw Malformed("Continuation token has trailing bytes for a null or unsorted value.");
                }

                return null;
            case CursorValueKind.Number:
                if (bytes.Length != 16)
                {
                    throw Malformed("Continuation token has an invalid length for a number value.");
                }

                var bits = new int[4];
                for (var i = 0; i < 4; i++)
                {
                    bits[i] = BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(i * 4, 4));
                }

                try
                {
                    return new decimal(bits);
                }
                catch (ArgumentException ex)
                {
                    throw Malformed("Continuation token has an invalid number value.", ex);
                }

            case CursorValueKind.DateTime:
                if (bytes.Length != 8)
                {
                    throw Malformed("Continuation token has an invalid length for a datetime value.");
                }

                var ticks = BinaryPrimitives.ReadInt64LittleEndian(bytes);
                if (ticks < 0 || ticks > DateTime.MaxValue.Ticks)
                {
                    throw Malformed("Continuation token has an out-of-range datetime value.");
                }

                return new DateTimeOffset(ticks, TimeSpan.Zero);
            case CursorValueKind.Boolean:
                if (bytes.Length != 1)
                {
                    throw Malformed("Continuation token has an invalid length for a boolean value.");
                }

                return bytes[0] switch
                {
                    0 => false,
                    1 => true,
                    _ => throw Malformed("Continuation token has an invalid boolean value.")
                };
            case CursorValueKind.Guid:
                if (bytes.Length != 16)
                {
                    throw Malformed("Continuation token has an invalid length for a guid value.");
                }

                return new Guid(bytes, bigEndian: false);
            case CursorValueKind.String:
                if (bytes.Length < 2)
                {
                    throw Malformed("Continuation token is too short for a string value's length prefix.");
                }

                var stringByteLength = BinaryPrimitives.ReadUInt16LittleEndian(bytes);
                if (bytes.Length != 2 + stringByteLength)
                {
                    throw Malformed("Continuation token's string value length prefix does not match the remaining bytes.");
                }

                string decoded;
                try
                {
                    decoded = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString(bytes[2..]);
                }
                catch (DecoderFallbackException ex)
                {
                    throw Malformed("Continuation token has invalid UTF-8 in its string value.", ex);
                }

                if (decoded.Contains('\0', StringComparison.Ordinal))
                {
                    throw Malformed("Continuation token has a null character (U+0000) in its string value.");
                }

                if (decoded.Length > SearchFieldValueLimits.MaxStringValueLength)
                {
                    throw Malformed(
                        $"Continuation token has a string value longer than the {SearchFieldValueLimits.MaxStringValueLength}-character limit for indexed string values.");
                }

                return decoded;

            default:
                throw Malformed($"Continuation token has an unknown sort value kind: {kind}.");
        }
    }

    private static InvalidContinuationTokenException Malformed(string message, Exception? inner = null) =>
        new(InvalidContinuationTokenReason.Malformed, message, inner);
}
