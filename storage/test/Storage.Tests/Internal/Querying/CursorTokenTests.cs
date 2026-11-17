// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Buffers.Text;
using Duende.Storage.Internal.Querying.Fields;
using Duende.Storage.Internal.Querying.SearchFields;
using Duende.Storage.Internal.Querying.Sorting;
using Duende.Storage.Internal.Pagination;

namespace Duende.Storage.Internal.Querying.Cursors;

public sealed class CursorTokenTests
{
    private static readonly EntityType EntityType = new(1, "Test");
    private static readonly EntityType OtherEntityType = new(2, "Other");

    private static readonly StringField StringField = new("name");
    private static readonly NumberField NumberField = new("rank");
    private static readonly DateTimeField DateTimeFieldInstance = new("when");
    private static readonly BooleanField BooleanFieldInstance = new("flag");
    private static readonly GuidField GuidFieldInstance = new("gid");

    private static readonly SortParameter StringSortAsc = new(StringField);
    private static readonly SortParameter StringSortDesc = new(StringField, Duende.Storage.Internal.Querying.SortDirection.Descending);
    private static readonly SortParameter NumberSortAsc = new(NumberField);
    private static readonly SortParameter DateTimeSortAsc = new(DateTimeFieldInstance);
    private static readonly SortParameter BooleanSortAsc = new(BooleanFieldInstance);
    private static readonly SortParameter GuidSortAsc = new(GuidFieldInstance);

    // --- Round trip tests ---

    [Fact]
    public void round_trips_start_forward()
    {
        var token = CursorToken.ForStart(CursorDirection.Forward, EntityType, SortParameter.Empty).Encode();
        var decoded = CursorToken.Decode(token, SortParameter.Empty, EntityType);

        decoded.Direction.ShouldBe(CursorDirection.Forward);
        decoded.Position.Kind.ShouldBe(CursorPositionKind.Start);
    }

    [Fact]
    public void round_trips_start_backward()
    {
        var token = CursorToken.ForStart(CursorDirection.Backward, EntityType, StringSortAsc).Encode();
        var decoded = CursorToken.Decode(token, StringSortAsc, EntityType);

        decoded.Direction.ShouldBe(CursorDirection.Backward);
        decoded.Position.Kind.ShouldBe(CursorPositionKind.Start);
    }

    [Fact]
    public void round_trips_unsorted_key()
    {
        var id = Guid.NewGuid();
        var token = CursorToken.ForPage(CursorDirection.Forward, EntityType, SortParameter.Empty, id, null).Encode();
        var decoded = CursorToken.Decode(token, SortParameter.Empty, EntityType);

        decoded.Position.Kind.ShouldBe(CursorPositionKind.Key);
        decoded.Position.Id.ShouldBe(id);
        decoded.Position.ValueKind.ShouldBe(CursorValueKind.None);
        decoded.Position.Value.ShouldBeNull();
    }

    [Fact]
    public void round_trips_null_sort_value()
    {
        var id = Guid.NewGuid();
        var token = CursorToken.ForPage(CursorDirection.Forward, EntityType, StringSortAsc, id, null).Encode();
        var decoded = CursorToken.Decode(token, StringSortAsc, EntityType);

        decoded.Position.ValueKind.ShouldBe(CursorValueKind.Null);
        decoded.Position.Value.ShouldBeNull();
        decoded.Position.Id.ShouldBe(id);
    }

    [Theory]
    [InlineData("hello")]
    [InlineData("")]
    [InlineData("caf\u00e9 \ud83d\ude00 \u4e2d\u6587")]
    public void round_trips_string_sort_value(string value)
    {
        var id = Guid.NewGuid();
        var token = CursorToken.ForPage(CursorDirection.Forward, EntityType, StringSortAsc, id, value).Encode();
        var decoded = CursorToken.Decode(token, StringSortAsc, EntityType);

        decoded.Position.ValueKind.ShouldBe(CursorValueKind.String);
        decoded.Position.Value.ShouldBe(value);
    }

    [Theory]
    [InlineData("123.456")]
    [InlineData("-99999.00001")]
    [InlineData("0")]
    public void round_trips_number_sort_value(string decimalText)
    {
        var value = decimal.Parse(decimalText, System.Globalization.CultureInfo.InvariantCulture);
        var id = Guid.NewGuid();
        var token = CursorToken.ForPage(CursorDirection.Forward, EntityType, NumberSortAsc, id, value).Encode();
        var decoded = CursorToken.Decode(token, NumberSortAsc, EntityType);

        decoded.Position.ValueKind.ShouldBe(CursorValueKind.Number);
        decoded.Position.Value.ShouldBe(value);
    }

    [Fact]
    public void round_trips_datetime_sort_value_with_non_zero_offset_as_the_same_instant_at_offset_zero()
    {
        var value = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.FromHours(5));
        var id = Guid.NewGuid();
        var token = CursorToken.ForPage(CursorDirection.Forward, EntityType, DateTimeSortAsc, id, value).Encode();
        var decoded = CursorToken.Decode(token, DateTimeSortAsc, EntityType);

        decoded.Position.ValueKind.ShouldBe(CursorValueKind.DateTime);
        var decodedValue = decoded.Position.Value.ShouldBeOfType<DateTimeOffset>();
        decodedValue.Offset.ShouldBe(TimeSpan.Zero);
        decodedValue.UtcTicks.ShouldBe(value.UtcTicks);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void round_trips_bool_sort_value(bool value)
    {
        var id = Guid.NewGuid();
        var token = CursorToken.ForPage(CursorDirection.Forward, EntityType, BooleanSortAsc, id, value).Encode();
        var decoded = CursorToken.Decode(token, BooleanSortAsc, EntityType);

        decoded.Position.ValueKind.ShouldBe(CursorValueKind.Boolean);
        decoded.Position.Value.ShouldBe(value);
    }

    [Fact]
    public void round_trips_guid_sort_value()
    {
        var value = Guid.NewGuid();
        var id = Guid.NewGuid();
        var token = CursorToken.ForPage(CursorDirection.Forward, EntityType, GuidSortAsc, id, value).Encode();
        var decoded = CursorToken.Decode(token, GuidSortAsc, EntityType);

        decoded.Position.ValueKind.ShouldBe(CursorValueKind.Guid);
        decoded.Position.Value.ShouldBe(value);
    }

    // --- Lengths ---

    [Fact]
    public void start_token_is_14_chars()
    {
        var token = CursorToken.ForStart(CursorDirection.Forward, EntityType, SortParameter.Empty).Encode();
        token.Value.Length.ShouldBe(14);
    }

    [Fact]
    public void unsorted_key_token_is_35_chars()
    {
        var token = CursorToken.ForPage(CursorDirection.Forward, EntityType, SortParameter.Empty, Guid.NewGuid(), null).Encode();
        token.Value.Length.ShouldBe(35);
    }

    // --- Beginning ---

    [Fact]
    public void beginning_decodes_to_start_forward_regardless_of_sort()
    {
        var decoded = CursorToken.Decode(ContinuationToken.Create(ContinuationToken.Beginning), StringSortAsc, EntityType);

        decoded.Direction.ShouldBe(CursorDirection.Forward);
        decoded.Position.Kind.ShouldBe(CursorPositionKind.Start);
    }

    // --- Exact-length checks ---

    [Fact]
    public void start_token_truncated_by_one_byte_is_malformed()
    {
        var bytes = DecodeRaw(CursorToken.ForStart(CursorDirection.Forward, EntityType, SortParameter.Empty).Encode());
        var corrupted = EncodeRaw(bytes[..^1]);

        AssertMalformed(corrupted, SortParameter.Empty);
    }

    [Fact]
    public void start_token_extended_by_one_byte_is_malformed()
    {
        var bytes = DecodeRaw(CursorToken.ForStart(CursorDirection.Forward, EntityType, SortParameter.Empty).Encode());
        var corrupted = EncodeRaw([.. bytes, 0]);

        AssertMalformed(corrupted, SortParameter.Empty);
    }

    [Fact]
    public void unsorted_key_token_truncated_by_one_byte_is_malformed()
    {
        var bytes = DecodeRaw(CursorToken.ForPage(CursorDirection.Forward, EntityType, SortParameter.Empty, Guid.NewGuid(), null).Encode());
        var corrupted = EncodeRaw(bytes[..^1]);

        AssertMalformed(corrupted, SortParameter.Empty);
    }

    [Fact]
    public void unsorted_key_token_extended_by_one_byte_is_malformed()
    {
        var bytes = DecodeRaw(CursorToken.ForPage(CursorDirection.Forward, EntityType, SortParameter.Empty, Guid.NewGuid(), null).Encode());
        var corrupted = EncodeRaw([.. bytes, 0]);

        AssertMalformed(corrupted, SortParameter.Empty);
    }

    [Fact]
    public void number_token_truncated_by_one_byte_is_malformed()
    {
        var bytes = DecodeRaw(CursorToken.ForPage(CursorDirection.Forward, EntityType, NumberSortAsc, Guid.NewGuid(), 1m).Encode());
        AssertMalformed(EncodeRaw(bytes[..^1]), NumberSortAsc);
    }

    [Fact]
    public void number_token_extended_by_one_byte_is_malformed()
    {
        var bytes = DecodeRaw(CursorToken.ForPage(CursorDirection.Forward, EntityType, NumberSortAsc, Guid.NewGuid(), 1m).Encode());
        AssertMalformed(EncodeRaw([.. bytes, 0]), NumberSortAsc);
    }

    [Fact]
    public void datetime_token_truncated_by_one_byte_is_malformed()
    {
        var bytes = DecodeRaw(CursorToken.ForPage(CursorDirection.Forward, EntityType, DateTimeSortAsc, Guid.NewGuid(), DateTimeOffset.UtcNow).Encode());
        AssertMalformed(EncodeRaw(bytes[..^1]), DateTimeSortAsc);
    }

    [Fact]
    public void datetime_token_extended_by_one_byte_is_malformed()
    {
        var bytes = DecodeRaw(CursorToken.ForPage(CursorDirection.Forward, EntityType, DateTimeSortAsc, Guid.NewGuid(), DateTimeOffset.UtcNow).Encode());
        AssertMalformed(EncodeRaw([.. bytes, 0]), DateTimeSortAsc);
    }

    [Fact]
    public void bool_token_truncated_by_one_byte_is_malformed()
    {
        var bytes = DecodeRaw(CursorToken.ForPage(CursorDirection.Forward, EntityType, BooleanSortAsc, Guid.NewGuid(), true).Encode());
        AssertMalformed(EncodeRaw(bytes[..^1]), BooleanSortAsc);
    }

    [Fact]
    public void bool_token_extended_by_one_byte_is_malformed()
    {
        var bytes = DecodeRaw(CursorToken.ForPage(CursorDirection.Forward, EntityType, BooleanSortAsc, Guid.NewGuid(), true).Encode());
        AssertMalformed(EncodeRaw([.. bytes, 0]), BooleanSortAsc);
    }

    [Fact]
    public void guid_token_truncated_by_one_byte_is_malformed()
    {
        var bytes = DecodeRaw(CursorToken.ForPage(CursorDirection.Forward, EntityType, GuidSortAsc, Guid.NewGuid(), Guid.NewGuid()).Encode());
        AssertMalformed(EncodeRaw(bytes[..^1]), GuidSortAsc);
    }

    [Fact]
    public void guid_token_extended_by_one_byte_is_malformed()
    {
        var bytes = DecodeRaw(CursorToken.ForPage(CursorDirection.Forward, EntityType, GuidSortAsc, Guid.NewGuid(), Guid.NewGuid()).Encode());
        AssertMalformed(EncodeRaw([.. bytes, 0]), GuidSortAsc);
    }

    [Fact]
    public void string_token_truncated_by_one_byte_is_malformed()
    {
        // Truncating by one byte makes the remaining bytes shorter than the length prefix promises.
        var bytes = DecodeRaw(CursorToken.ForPage(CursorDirection.Forward, EntityType, StringSortAsc, Guid.NewGuid(), "cafe\u00e9").Encode());
        AssertMalformed(EncodeRaw(bytes[..^1]), StringSortAsc);
    }

    [Fact]
    public void string_token_extended_by_one_byte_is_malformed()
    {
        // Extending by one byte makes the remaining bytes longer than the length prefix promises.
        var bytes = DecodeRaw(CursorToken.ForPage(CursorDirection.Forward, EntityType, StringSortAsc, Guid.NewGuid(), "cafe\u00e9").Encode());
        AssertMalformed(EncodeRaw([.. bytes, 0]), StringSortAsc);
    }

    [Fact]
    public void string_token_with_a_length_prefix_that_disagrees_with_the_remaining_bytes_is_malformed()
    {
        // The length prefix itself is wrong (too large), even though the total buffer length wasn't
        // simply truncated or extended by encode/decode: a hand-crafted token could claim any length.
        var bytes = DecodeRaw(CursorToken.ForPage(CursorDirection.Forward, EntityType, StringSortAsc, Guid.NewGuid(), "cafe").Encode());
        var valueOffset = bytes.Length - 4 /* "cafe" */ - 2 /* length prefix */;
        var originalLength = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(valueOffset, 2));
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(valueOffset, 2), (ushort)(originalLength + 1));

        AssertMalformed(EncodeRaw(bytes), StringSortAsc);
    }

    // --- Reserved bits, unknown kind, bad bool byte, invalid UTF-8 ---

    [Fact]
    public void reserved_flag_bits_set_is_malformed()
    {
        var bytes = DecodeRaw(CursorToken.ForStart(CursorDirection.Forward, EntityType, SortParameter.Empty).Encode());
        bytes[1] |= 0b1000_0000;

        AssertMalformed(EncodeRaw(bytes), SortParameter.Empty);
    }

    [Fact]
    public void unknown_value_kind_is_malformed()
    {
        var bytes = DecodeRaw(CursorToken.ForPage(CursorDirection.Forward, EntityType, SortParameter.Empty, Guid.NewGuid(), null).Encode());
        // bits 2-4 = 111 (7), an unknown kind.
        bytes[1] = (byte)((bytes[1] & 0b1110_0011) | 0b0001_1100);

        AssertMalformed(EncodeRaw(bytes), SortParameter.Empty);
    }

    [Fact]
    public void bool_byte_value_of_two_is_malformed()
    {
        var bytes = DecodeRaw(CursorToken.ForPage(CursorDirection.Forward, EntityType, BooleanSortAsc, Guid.NewGuid(), true).Encode());
        bytes[^1] = 2;

        AssertMalformed(EncodeRaw(bytes), BooleanSortAsc);
    }

    [Fact]
    public void invalid_utf8_in_string_value_is_malformed()
    {
        var bytes = DecodeRaw(CursorToken.ForPage(CursorDirection.Forward, EntityType, StringSortAsc, Guid.NewGuid(), "hello").Encode());
        bytes[^1] = 0xFF;

        AssertMalformed(EncodeRaw(bytes), StringSortAsc);
    }

    [Fact]
    public void null_character_in_decoded_string_value_is_malformed()
    {
        // F4: a decoded string value containing U+0000 is rejected, even though it round-trips through
        // UTF-8 cleanly (unlike invalid_utf8_in_string_value_is_malformed above). Encode() now refuses
        // to produce such a token itself, so craft the bytes directly to exercise the decode-side guard.
        var bytes = DecodeRaw(CursorToken.ForPage(CursorDirection.Forward, EntityType, StringSortAsc, Guid.NewGuid(), "hellx").Encode());
        bytes[^1] = 0;

        AssertMalformed(EncodeRaw(bytes), StringSortAsc);
    }

    // --- Bad Base64Url / oversized ---

    [Fact]
    public void illegal_base64url_characters_are_malformed()
    {
        var token = ContinuationToken.Create("!!!!not-base64url!!!!");
        AssertMalformed(token, SortParameter.Empty);
    }

    [Fact]
    public void standard_base64_padding_is_malformed()
    {
        // Standard Base64 padding characters ('=') are not valid Base64Url.
        var token = ContinuationToken.Create("AAAA====");
        AssertMalformed(token, SortParameter.Empty);
    }

    [Fact]
    public void oversized_input_is_malformed()
    {
        var token = ContinuationToken.Create(new string('A', CursorToken.MaxEncodedChars + 1));
        AssertMalformed(token, SortParameter.Empty);
    }

    [Fact]
    public void round_trips_max_length_string_sort_value_of_surrogate_pairs()
    {
        // SearchFieldValueLimits.MaxStringValueLength bounds a string's .Length (UTF-16 code units).
        // 250 emoji, each a surrogate pair, is exactly 500 UTF-16 units (1000 UTF-8 bytes): the longest
        // string of 4-byte-UTF-8 surrogate pairs that still fits the character limit.
        var value = string.Concat(Enumerable.Repeat("\ud83d\ude00", SearchFieldValueLimits.MaxStringValueLength / 2));
        value.Length.ShouldBe(SearchFieldValueLimits.MaxStringValueLength);
        var id = Guid.NewGuid();

        var token = CursorToken.ForPage(CursorDirection.Forward, EntityType, StringSortAsc, id, value).Encode();
        var decoded = CursorToken.Decode(token, StringSortAsc, EntityType);

        decoded.Position.ValueKind.ShouldBe(CursorValueKind.String);
        decoded.Position.Value.ShouldBe(value);
    }

    [Fact]
    public void round_trips_max_length_string_sort_value_of_three_byte_bmp_characters()
    {
        // 500 three-byte BMP characters (1500 UTF-8 bytes) exercises the other worst case at the same
        // character-count limit: each code unit costs 3 bytes instead of 2 for a surrogate pair.
        var value = string.Concat(Enumerable.Repeat('\u20ac', SearchFieldValueLimits.MaxStringValueLength));
        value.Length.ShouldBe(SearchFieldValueLimits.MaxStringValueLength);
        var id = Guid.NewGuid();

        var token = CursorToken.ForPage(CursorDirection.Forward, EntityType, StringSortAsc, id, value).Encode();
        var decoded = CursorToken.Decode(token, StringSortAsc, EntityType);

        decoded.Position.ValueKind.ShouldBe(CursorValueKind.String);
        decoded.Position.Value.ShouldBe(value);
    }

    [Fact]
    public void encode_refuses_a_string_sort_value_longer_than_the_maximum()
    {
        // Exceeds CursorToken.MaxStringValueByteLength (SearchFieldValueLimits.MaxStringValueLength * 4
        // bytes), regardless of the string's .Length in UTF-16 units.
        var value = string.Concat(Enumerable.Repeat("\ud83d\ude00", SearchFieldValueLimits.MaxStringValueLength + 1));
        var id = Guid.NewGuid();
        var token = CursorToken.ForPage(CursorDirection.Forward, EntityType, StringSortAsc, id, value);

        var ex = Should.Throw<InvalidOperationException>(() => token.Encode());
        ex.Message.ShouldContain("500");
    }

    [Fact]
    public void encode_refuses_a_string_sort_value_containing_a_null_character()
    {
        var id = Guid.NewGuid();
        var token = CursorToken.ForPage(CursorDirection.Forward, EntityType, StringSortAsc, id, "hel\0lo");

        var ex = Should.Throw<InvalidOperationException>(() => token.Encode());
        ex.Message.ShouldContain("null character");
    }

    [Fact]
    public void round_trips_max_length_ascii_string_sort_value()
    {
        var value = new string('a', SearchFieldValueLimits.MaxStringValueLength);
        var id = Guid.NewGuid();

        var token = CursorToken.ForPage(CursorDirection.Forward, EntityType, StringSortAsc, id, value).Encode();
        var decoded = CursorToken.Decode(token, StringSortAsc, EntityType);

        decoded.Position.ValueKind.ShouldBe(CursorValueKind.String);
        decoded.Position.Value.ShouldBe(value);
    }

    [Fact]
    public void encode_refuses_an_ascii_string_sort_value_one_character_over_the_character_limit()
    {
        // F1: the byte-length guard alone would miss this - 501 single-byte ASCII characters fit
        // well under CursorToken.MaxStringValueByteLength (500 * 4 bytes), but still exceed the
        // 500-character limit measured in UTF-16 code units.
        var value = new string('a', SearchFieldValueLimits.MaxStringValueLength + 1);
        var id = Guid.NewGuid();
        var token = CursorToken.ForPage(CursorDirection.Forward, EntityType, StringSortAsc, id, value);

        var ex = Should.Throw<InvalidOperationException>(() => token.Encode());
        ex.Message.ShouldContain("500");
    }

    [Fact]
    public void decoded_ascii_string_sort_value_one_character_over_the_character_limit_is_malformed()
    {
        // F1 (decode side): craft a buffer whose string value is 501 single-byte ASCII characters,
        // bypassing CursorToken.Encode()'s own guard, to prove Decode independently rejects a
        // string value longer than the 500-character limit.
        var fingerprint = SortFingerprint.Compute(EntityType, StringSortAsc);
        var id = Guid.NewGuid();
        var value = new string('a', SearchFieldValueLimits.MaxStringValueLength + 1);

        var bytes = new List<byte> { 1, (byte)((((int)CursorValueKind.String) << 2) | 0b0000_0010) };
        bytes.AddRange(fingerprint);
        bytes.AddRange(id.ToByteArray());
        var valueBytes = System.Text.Encoding.UTF8.GetBytes(value);
        var lengthPrefix = new byte[2];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(lengthPrefix, (ushort)valueBytes.Length);
        bytes.AddRange(lengthPrefix);
        bytes.AddRange(valueBytes);

        var token = EncodeRaw([.. bytes]);

        AssertMalformed(token, StringSortAsc);
    }

    // --- Version byte ---

    [Fact]
    public void version_byte_zero_is_unsupported_version()
    {
        var bytes = DecodeRaw(CursorToken.ForStart(CursorDirection.Forward, EntityType, SortParameter.Empty).Encode());
        bytes[0] = 0;

        var ex = Should.Throw<InvalidContinuationTokenException>(() => CursorToken.Decode(EncodeRaw(bytes), SortParameter.Empty, EntityType));
        ex.Reason.ShouldBe(InvalidContinuationTokenReason.UnsupportedVersion);
    }

    [Fact]
    public void version_byte_two_is_unsupported_version()
    {
        var bytes = DecodeRaw(CursorToken.ForStart(CursorDirection.Forward, EntityType, SortParameter.Empty).Encode());
        bytes[0] = 2;

        var ex = Should.Throw<InvalidContinuationTokenException>(() => CursorToken.Decode(EncodeRaw(bytes), SortParameter.Empty, EntityType));
        ex.Reason.ShouldBe(InvalidContinuationTokenReason.UnsupportedVersion);
    }

    // --- Fingerprint mismatch ---

    [Fact]
    public void field_mismatch_is_sort_mismatch()
    {
        var token = CursorToken.ForPage(CursorDirection.Forward, EntityType, StringSortAsc, Guid.NewGuid(), "a").Encode();
        var otherStringField = new SortParameter(new StringField("other"));

        var ex = Should.Throw<InvalidContinuationTokenException>(() => CursorToken.Decode(token, otherStringField, EntityType));
        ex.Reason.ShouldBe(InvalidContinuationTokenReason.SortMismatch);
    }

    [Fact]
    public void direction_mismatch_is_sort_mismatch()
    {
        var token = CursorToken.ForPage(CursorDirection.Forward, EntityType, StringSortAsc, Guid.NewGuid(), "a").Encode();

        var ex = Should.Throw<InvalidContinuationTokenException>(() => CursorToken.Decode(token, StringSortDesc, EntityType));
        ex.Reason.ShouldBe(InvalidContinuationTokenReason.SortMismatch);
    }

    [Fact]
    public void entity_type_mismatch_is_sort_mismatch()
    {
        var token = CursorToken.ForPage(CursorDirection.Forward, EntityType, StringSortAsc, Guid.NewGuid(), "a").Encode();

        var ex = Should.Throw<InvalidContinuationTokenException>(() => CursorToken.Decode(token, StringSortAsc, OtherEntityType));
        ex.Reason.ShouldBe(InvalidContinuationTokenReason.SortMismatch);
    }

    [Fact]
    public void kind_mismatch_with_matching_fingerprint_is_sort_mismatch()
    {
        // Craft a buffer whose fingerprint matches NumberSortAsc, but whose value kind bits say Guid.
        var fingerprint = SortFingerprint.Compute(EntityType, NumberSortAsc);
        var id = Guid.NewGuid();
        var value = Guid.NewGuid();

        var bytes = new List<byte> { 1, (byte)(0 | (((int)CursorValueKind.Guid) << 2) | 0b0000_0010) };
        bytes.AddRange(fingerprint);
        bytes.AddRange(id.ToByteArray());
        bytes.AddRange(value.ToByteArray());

        var token = EncodeRaw([.. bytes]);

        var ex = Should.Throw<InvalidContinuationTokenException>(() => CursorToken.Decode(token, NumberSortAsc, EntityType));
        ex.Reason.ShouldBe(InvalidContinuationTokenReason.SortMismatch);
    }

    // --- Old JSON-format tokens ---

    [Fact]
    public void old_json_token_with_special_base64_characters_is_rejected()
    {
        var oldJson = $$"""{"Id":"{{Guid.NewGuid()}}","StringValue":null,"NumberValue":null,"DateTimeValue":null,"BooleanValue":null,"GuidValue":null}""";
        var encoded = ForceSpecialBase64Character(Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(oldJson)));

        var ex = Should.Throw<InvalidContinuationTokenException>(() => CursorToken.Decode(ContinuationToken.Create(encoded), SortParameter.Empty, EntityType));
        (ex.Reason is InvalidContinuationTokenReason.Malformed or InvalidContinuationTokenReason.UnsupportedVersion).ShouldBeTrue();
    }

    [Fact]
    public void old_json_token_without_special_base64_characters_is_rejected()
    {
        var oldJson = $$"""{"Id":"{{Guid.NewGuid()}}","StringValue":null,"NumberValue":null,"DateTimeValue":null,"BooleanValue":null,"GuidValue":null}""";
        var encoded = RemoveSpecialBase64Characters(Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(oldJson)));
        encoded.IndexOfAny(['+', '/', '=']).ShouldBeLessThan(0);

        var ex = Should.Throw<InvalidContinuationTokenException>(() => CursorToken.Decode(ContinuationToken.Create(encoded), SortParameter.Empty, EntityType));
        (ex.Reason is InvalidContinuationTokenReason.Malformed or InvalidContinuationTokenReason.UnsupportedVersion).ShouldBeTrue();
    }

    /// <summary>
    /// Forces the first character of a standard-Base64 string to '+', one of the characters that is
    /// valid in standard Base64 but not in Base64Url.
    /// </summary>
    private static string ForceSpecialBase64Character(string standardBase64) => "+" + standardBase64[1..];

    /// <summary>
    /// Replaces any standard-Base64-only characters ('+', '/', '=') with a character valid in both
    /// alphabets, so the remaining string has none of them.
    /// </summary>
    private static string RemoveSpecialBase64Characters(string standardBase64) =>
        standardBase64.Replace('+', 'A').Replace('/', 'A').Replace('=', 'A');

    // --- Helpers ---

    private static void AssertMalformed(ContinuationToken token, SortParameter sort)
    {
        var ex = Should.Throw<InvalidContinuationTokenException>(() => CursorToken.Decode(token, sort, EntityType));
        ex.Reason.ShouldBe(InvalidContinuationTokenReason.Malformed);
    }

    private static byte[] DecodeRaw(ContinuationToken token)
    {
        var chars = token.Value;
        var buffer = new byte[Base64Url.GetMaxDecodedLength(chars.Length)];
        var success = Base64Url.TryDecodeFromChars(chars, buffer, out var written);
        success.ShouldBeTrue();
        return buffer[..written];
    }

    private static ContinuationToken EncodeRaw(byte[] bytes) => ContinuationToken.Create(Base64Url.EncodeToString(bytes));
}
