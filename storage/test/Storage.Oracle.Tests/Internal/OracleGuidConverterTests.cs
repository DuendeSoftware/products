// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.Oracle.Internal;

public sealed class OracleGuidConverterTests
{
    [Fact]
    public void Round_trip_preserves_original_value()
    {
        var original = Guid.CreateVersion7();

        var raw = OracleGuidConverter.ToRaw(original);
        var restored = OracleGuidConverter.FromRaw(raw);

        restored.ShouldBe(original);
    }

    [Fact]
    public void To_raw_produces_canonical_big_endian_bytes()
    {
        var original = Guid.CreateVersion7();
        Span<byte> expected = stackalloc byte[16];
        _ = original.TryWriteBytes(expected, bigEndian: true, out _);

        var raw = OracleGuidConverter.ToRaw(original);

        raw.Length.ShouldBe(16);
        raw.ShouldBe(expected.ToArray());
    }

    [Fact]
    public void Chronological_order_preserved_in_raw_format()
    {
        // UUIDv7 places its 48-bit timestamp in the leading (big-endian) bytes, so the
        // big-endian RAW(16) representation must sort chronologically under Oracle's
        // binary RAW comparison (lexicographic byte comparison).
        var baseTime = DateTimeOffset.UtcNow;
        var raws = new List<byte[]>();
        for (var i = 0; i < 50; i++)
        {
            var id = Guid.CreateVersion7(baseTime.AddMilliseconds(i));
            raws.Add(OracleGuidConverter.ToRaw(id));
        }

        for (var i = 1; i < raws.Count; i++)
        {
            var comparison = ((ReadOnlySpan<byte>)raws[i - 1]).SequenceCompareTo(raws[i]);
            comparison.ShouldBeLessThan(0,
                $"Expected RAW at index {i - 1} to sort before index {i} in binary order.");
        }
    }

    [Fact]
    public void Batch_round_trip_all_values_preserved()
    {
        var originals = Enumerable.Range(0, 1000)
            .Select(_ => Guid.CreateVersion7())
            .ToList();

        var roundTripped = originals
            .Select(OracleGuidConverter.ToRaw)
            .Select(OracleGuidConverter.FromRaw)
            .ToList();

        for (var i = 0; i < originals.Count; i++)
        {
            roundTripped[i].ShouldBe(originals[i], $"Mismatch at index {i}");
        }
    }
}
