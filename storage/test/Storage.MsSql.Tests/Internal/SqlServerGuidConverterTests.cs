// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Data.SqlTypes;

namespace Duende.Storage.MsSql.Internal;

public sealed class SqlServerGuidConverterTests
{
    [Fact]
    public void Round_trip_preserves_original_value()
    {
        var original = Guid.CreateVersion7();

        var sqlServer = SqlServerGuidConverter.ToSqlServer(original);
        var restored = SqlServerGuidConverter.ToUuidV7(sqlServer);

        restored.ShouldBe(original);
    }

    [Fact]
    public void Round_trip_from_sql_server_preserves_value()
    {
        var original = Guid.CreateVersion7();
        var sqlServer = SqlServerGuidConverter.ToSqlServer(original);

        var restored = SqlServerGuidConverter.ToUuidV7(sqlServer);
        var backToSqlServer = SqlServerGuidConverter.ToSqlServer(restored);

        backToSqlServer.ShouldBe(sqlServer);
    }

    [Fact]
    public void Converted_value_differs_from_original()
    {
        // Use a known UUIDv7 with distinct byte groups to guarantee the swap produces a different value.
        var original = Guid.Parse("019d774c-58e8-78fa-b7d0-6f4c1a4ae935");

        var sqlServer = SqlServerGuidConverter.ToSqlServer(original);

        sqlServer.ShouldNotBe(original);
    }

    [Fact]
    public void Chronological_order_preserved_in_sql_server_format()
    {
        // Generate UUIDv7 values with deterministic, increasing timestamps.
        // SqlGuid implements SQL Server's actual UNIQUEIDENTIFIER comparison semantics,
        // so we use it as the authoritative sort-order reference.
        var baseTime = DateTimeOffset.UtcNow;
        var ids = new List<(Guid Original, Guid SqlServer)>();
        for (var i = 0; i < 50; i++)
        {
            var timestamp = baseTime.AddMilliseconds(i);
            var original = Guid.CreateVersion7(timestamp);
            ids.Add((original, SqlServerGuidConverter.ToSqlServer(original)));
        }

        // Verify that SqlGuid comparison preserves chronological order.
        for (var i = 1; i < ids.Count; i++)
        {
            var previous = new SqlGuid(ids[i - 1].SqlServer);
            var current = new SqlGuid(ids[i].SqlServer);

            var comparison = previous.CompareTo(current);
            comparison.ShouldBeLessThan(0,
                $"Expected GUID at index {i - 1} ({ids[i - 1].Original}) to sort before index {i} ({ids[i].Original}) " +
                $"in SQL Server order. SqlServer values: {previous} vs {current}");
        }
    }

    [Fact]
    public void Multiple_round_trips_produce_same_result()
    {
        var original = Guid.CreateVersion7();

        var sqlServer1 = SqlServerGuidConverter.ToSqlServer(original);
        var restored1 = SqlServerGuidConverter.ToUuidV7(sqlServer1);
        var sqlServer2 = SqlServerGuidConverter.ToSqlServer(restored1);
        var restored2 = SqlServerGuidConverter.ToUuidV7(sqlServer2);

        restored1.ShouldBe(original);
        restored2.ShouldBe(original);
        sqlServer1.ShouldBe(sqlServer2);
    }

    [Fact]
    public void Batch_round_trip_all_values_preserved()
    {
        var originals = Enumerable.Range(0, 1000)
            .Select(_ => Guid.CreateVersion7())
            .ToList();

        var roundTripped = originals
            .Select(SqlServerGuidConverter.ToSqlServer)
            .Select(SqlServerGuidConverter.ToUuidV7)
            .ToList();

        for (var i = 0; i < originals.Count; i++)
        {
            roundTripped[i].ShouldBe(originals[i], $"Mismatch at index {i}");
        }
    }

    [Fact]
    public void Timestamp_bytes_moved_to_high_priority_position()
    {
        var original = Guid.CreateVersion7();
        Span<byte> originalBytes = stackalloc byte[16];
        _ = original.TryWriteBytes(originalBytes, bigEndian: true, out _);

        var sqlServer = SqlServerGuidConverter.ToSqlServer(original);
        Span<byte> sqlBytes = stackalloc byte[16];
        _ = sqlServer.TryWriteBytes(sqlBytes, bigEndian: true, out _);

        // Timestamp (original bytes 0-5) should now be at bytes 10-15
        for (var i = 0; i < 6; i++)
        {
            sqlBytes[10 + i].ShouldBe(originalBytes[i],
                $"Timestamp byte {i} not correctly placed at SQL Server position {10 + i}");
        }
    }
}
