// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.IntegrationTests;
using Duende.Storage.Internal;
using Npgsql;

namespace Duende.Storage.PostgreSql;

/// <summary>
/// PostgreSQL's raw second connection/transaction for <see cref="StoreLinkDeterministicConcurrencyTests"/>:
/// row locks via <c>SELECT ... FOR UPDATE</c>, blocking detection via <c>pg_locks</c>/
/// <c>pg_blocking_pids</c>, and deadlock victim control via this session's own <c>deadlock_timeout</c>.
/// </summary>
internal sealed class PostgreSqlConcurrencyTestFixture : IConcurrencyTestFixture
{
    public async Task<IConcurrencySession> BeginSessionAsync(IMigrationFixture fixture, Ct ct)
    {
        var pgFixture = (PostgreSqlMigrationFixture)fixture;
        var connection = await pgFixture.DataSource.OpenConnectionAsync(ct);
        await using (var searchPathCmd = connection.CreateCommand())
        {
            searchPathCmd.CommandText = $"SET search_path TO \"{pgFixture.SchemaName}\"";
            _ = await searchPathCmd.ExecuteNonQueryAsync(ct);
        }

        var transaction = await connection.BeginTransactionAsync(ct);

        int backendPid;
        await using (var pidCmd = connection.CreateCommand())
        {
            pidCmd.Transaction = transaction;
            pidCmd.CommandText = "SELECT pg_backend_pid()";
            backendPid = (int)(await pidCmd.ExecuteScalarAsync(ct))!;
        }

        return new PostgreSqlConcurrencySession(pgFixture.DataSource, connection, transaction, backendPid);
    }
}

internal sealed class PostgreSqlConcurrencySession(
    NpgsqlDataSource dataSource,
    NpgsqlConnection connection,
    NpgsqlTransaction transaction,
    int backendPid) : IConcurrencySession
{
    public async Task LockEntityRowAsync(EntityType entityType, Guid entityId, Ct ct)
    {
        await using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = """
            SELECT 1 FROM entities
            WHERE pool_id = @pool_id AND entity_type_id = @entity_type_id AND entity_id = @entity_id
            FOR UPDATE
            """;
        _ = cmd.Parameters.AddWithValue("@pool_id", PoolId.Default.Value);
        _ = cmd.Parameters.AddWithValue("@entity_type_id", (int)entityType.Id);
        _ = cmd.Parameters.AddWithValue("@entity_id", entityId);
        var locked = await cmd.ExecuteScalarAsync(ct);
        if (locked is null)
        {
            throw new InvalidOperationException($"No entities row matched (pool_id={PoolId.Default.Value}, entity_type_id={(int)entityType.Id}, entity_id={entityId}) to lock.");
        }

        // Confirm the lock is visible server-side (via pg_locks) before returning: a caller that
        // immediately races a conflicting statement on another connection must see a lock that has
        // actually landed, not just a completed ExecuteScalarAsync on this connection.
        await using var verifyCmd = connection.CreateCommand();
        verifyCmd.CommandText = "SELECT COUNT(*) FROM pg_locks WHERE pid = @pid AND granted";
        _ = verifyCmd.Parameters.AddWithValue("@pid", backendPid);
        var lockCount = (long)(await verifyCmd.ExecuteScalarAsync(ct))!;
        if (lockCount == 0)
        {
            throw new InvalidOperationException("The row lock was requested but is not visible in pg_locks yet.");
        }
    }

    public async Task InsertCascadeLinkAsync(LinkDefinition definition, UuidV7 left, UuidV7 right, Ct ct)
    {
        await using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = """
            INSERT INTO entity_links (pool_id, link_type_id, left_entity_type_id, left_entity_id, right_entity_type_id, right_entity_id, restrict_delete)
            VALUES (@pool_id, @link_type_id, @left_entity_type_id, @left_entity_id, @right_entity_type_id, @right_entity_id, false)
            """;
        _ = cmd.Parameters.AddWithValue("@pool_id", PoolId.Default.Value);
        _ = cmd.Parameters.AddWithValue("@link_type_id", (int)definition.Link.Id);
        _ = cmd.Parameters.AddWithValue("@left_entity_type_id", (int)definition.Left.Id);
        _ = cmd.Parameters.AddWithValue("@left_entity_id", left.Value);
        _ = cmd.Parameters.AddWithValue("@right_entity_type_id", (int)definition.Right.Id);
        _ = cmd.Parameters.AddWithValue("@right_entity_id", right.Value);
        _ = await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task WaitUntilBlockingAnotherSessionAsync(Ct ct)
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < deadline)
        {
            await using var freshConnection = await dataSource.OpenConnectionAsync(ct);
            await using var cmd = freshConnection.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM pg_stat_activity a WHERE @pid = ANY(pg_blocking_pids(a.pid))";
            _ = cmd.Parameters.AddWithValue("@pid", backendPid);
            var blocked = (long)(await cmd.ExecuteScalarAsync(ct))!;
            if (blocked > 0)
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100), ct);
        }

        await using var dbgConnection = await dataSource.OpenConnectionAsync(ct);
        await using var dbgCmd = dbgConnection.CreateCommand();
        dbgCmd.CommandText = "SELECT pid, state, wait_event_type, wait_event, left(query, 150) FROM pg_stat_activity ORDER BY pid";
        var rows = new List<string>();
        await using (var reader = await dbgCmd.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                rows.Add($"pid={reader.GetInt32(0)} state={(reader.IsDBNull(1) ? "" : reader.GetString(1))} wait={(reader.IsDBNull(2) ? "" : reader.GetString(2))}/{(reader.IsDBNull(3) ? "" : reader.GetString(3))} query={(reader.IsDBNull(4) ? "" : reader.GetString(4))}");
            }
        }

        throw new TimeoutException(
            "Timed out waiting for another session to be blocked by this session's lock (via pg_blocking_pids); the test's synchronization assumption may no longer hold. "
            + $"This session backendPid={backendPid}. pg_stat_activity: {string.Join(" || ", rows)}");
    }

    public async Task PreferOtherSessionAsDeadlockVictimAsync(Ct ct)
    {
        // PostgreSQL has no per-session deadlock priority: each backend only checks for a deadlock
        // once per lock wait, via a timer armed when that wait starts (ProcSleep), and runs that
        // check again every deadlock_timeout if still waiting. Raising this session's timeout well
        // above the server default (1s) normally means the engine's backend, which started waiting
        // first, hits its own (shorter) timeout first and becomes the victim. It isn't a guarantee:
        // if the engine's backend had already been waiting on some other lock longer than its
        // deadlock_timeout before this session closes the cycle, its next check may fire before
        // this session's 10s is up and it becomes the victim instead — the caller retries a bounded
        // number of times to cover that case.
        await using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = "SET deadlock_timeout = '10s'";
        _ = await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<bool> TryLockEntityRowAsync(EntityType entityType, Guid entityId, Ct ct)
    {
        try
        {
            await LockEntityRowAsync(entityType, entityId, ct);
            return true;
        }
        catch (PostgresException ex) when (string.Equals(ex.SqlState, "40P01", StringComparison.Ordinal))
        {
            return false;
        }
    }

    public async Task CommitAsync(Ct ct)
    {
        await transaction.CommitAsync(ct);
        await transaction.DisposeAsync();
        await connection.DisposeAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await transaction.DisposeAsync();
        await connection.DisposeAsync();
    }
}
