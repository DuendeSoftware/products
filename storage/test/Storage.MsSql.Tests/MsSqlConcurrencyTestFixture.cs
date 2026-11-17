// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.IntegrationTests;
using Duende.Storage.Internal;
using Duende.Storage.MsSql.Internal;
using Microsoft.Data.SqlClient;

namespace Duende.Storage.MsSql;

/// <summary>
/// SQL Server's raw second connection/transaction for <see cref="StoreLinkDeterministicConcurrencyTests"/>:
/// row locks via <c>WITH (UPDLOCK, ROWLOCK, HOLDLOCK)</c>, blocking detection via
/// <c>sys.dm_exec_requests.blocking_session_id</c>, and deadlock victim control via
/// <c>SET DEADLOCK_PRIORITY HIGH</c> on this session.
/// </summary>
internal sealed class MsSqlConcurrencyTestFixture : IConcurrencyTestFixture
{
    public async Task<IConcurrencySession> BeginSessionAsync(IMigrationFixture fixture, Ct ct)
    {
        var msSqlFixture = (MsSqlMigrationFixture)fixture;
        var connection = new SqlConnection(msSqlFixture.ConnectionString);
        await connection.OpenAsync(ct);
        var transaction = (SqlTransaction)await connection.BeginTransactionAsync(ct);

        int spid;
        await using (var spidCmd = connection.CreateCommand())
        {
            spidCmd.Transaction = transaction;
            spidCmd.CommandText = "SELECT @@SPID";
            spid = (short)(await spidCmd.ExecuteScalarAsync(ct))!;
        }

        return new MsSqlConcurrencySession(msSqlFixture.ConnectionString, connection, transaction, msSqlFixture.SchemaName, spid);
    }
}

internal sealed class MsSqlConcurrencySession(
    string connectionString,
    SqlConnection connection,
    SqlTransaction transaction,
    string schemaName,
    int spid) : IConcurrencySession
{
    public async Task LockEntityRowAsync(EntityType entityType, Guid entityId, Ct ct)
    {
        await using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = $"""
            SELECT 1 FROM [{schemaName}].[entities] WITH (UPDLOCK, ROWLOCK, HOLDLOCK)
            WHERE pool_id = @pool_id AND entity_type_id = @entity_type_id AND entity_id = @entity_id
            """;
        _ = cmd.Parameters.AddWithValue("@pool_id", PoolId.Default.Value);
        _ = cmd.Parameters.AddWithValue("@entity_type_id", (int)entityType.Id);
        _ = cmd.Parameters.AddWithValue("@entity_id", SqlServerGuidConverter.ToSqlServer(entityId));
        var locked = await cmd.ExecuteScalarAsync(ct);
        if (locked is null)
        {
            throw new InvalidOperationException($"No entities row matched (pool_id={PoolId.Default.Value}, entity_type_id={(int)entityType.Id}, entity_id={entityId}) to lock.");
        }
    }

    public async Task InsertCascadeLinkAsync(LinkDefinition definition, UuidV7 left, UuidV7 right, Ct ct)
    {
        await using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = $"""
            INSERT INTO [{schemaName}].[entity_links] (pool_id, link_type_id, left_entity_type_id, left_entity_id, right_entity_type_id, right_entity_id, restrict_delete)
            VALUES (@pool_id, @link_type_id, @left_entity_type_id, @left_entity_id, @right_entity_type_id, @right_entity_id, 0)
            """;
        _ = cmd.Parameters.AddWithValue("@pool_id", PoolId.Default.Value);
        _ = cmd.Parameters.AddWithValue("@link_type_id", (int)definition.Link.Id);
        _ = cmd.Parameters.AddWithValue("@left_entity_type_id", (int)definition.Left.Id);
        _ = cmd.Parameters.AddWithValue("@left_entity_id", SqlServerGuidConverter.ToSqlServer(left.Value));
        _ = cmd.Parameters.AddWithValue("@right_entity_type_id", (int)definition.Right.Id);
        _ = cmd.Parameters.AddWithValue("@right_entity_id", SqlServerGuidConverter.ToSqlServer(right.Value));
        _ = await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task WaitUntilBlockingAnotherSessionAsync(Ct ct)
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < deadline)
        {
            await using var monitorConnection = new SqlConnection(connectionString);
            await monitorConnection.OpenAsync(ct);
            await using var cmd = monitorConnection.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM sys.dm_exec_requests WHERE blocking_session_id = @spid";
            _ = cmd.Parameters.AddWithValue("@spid", spid);
            var blocked = (int)(await cmd.ExecuteScalarAsync(ct))!;
            if (blocked > 0)
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100), ct);
        }

        throw new TimeoutException("Timed out waiting for another session to be blocked by this session's lock (via sys.dm_exec_requests.blocking_session_id); the test's synchronization assumption may no longer hold.");
    }

    public Task PreferOtherSessionAsDeadlockVictimAsync(Ct ct) =>
        // Raising this session's own deadlock priority makes the deadlock monitor pick the lower
        // (default) priority session - the engine's - as the victim instead.
        ExecuteNonQueryAsync("SET DEADLOCK_PRIORITY HIGH", ct);

    private async Task ExecuteNonQueryAsync(string sql, Ct ct)
    {
        await using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = sql;
        _ = await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<bool> TryLockEntityRowAsync(EntityType entityType, Guid entityId, Ct ct)
    {
        try
        {
            await LockEntityRowAsync(entityType, entityId, ct);
            return true;
        }
        catch (SqlException ex) when (ex.Number == 1205)
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
