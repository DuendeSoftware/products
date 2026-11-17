// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.IntegrationTests;
using Duende.Storage.Internal;
using Duende.Storage.Oracle.Internal;
using Oracle.ManagedDataAccess.Client;

namespace Duende.Storage.Oracle;

/// <summary>
/// Oracle's raw second connection/transaction for <see cref="StoreLinkDeterministicConcurrencyTests"/>:
/// row locks via <c>SELECT ... FOR UPDATE</c>, blocking detection via <c>v$session.blocking_session</c>
/// (queried through a privileged connection, since the per-test user isn't granted <c>v$session</c>),
/// and deadlock victim control is a documented no-op — Oracle always signals the session whose
/// statement request completes the wait cycle, which this schedule arranges to be the engine's.
/// </summary>
internal sealed class OracleConcurrencyTestFixture : IConcurrencyTestFixture
{
    public async Task<IConcurrencySession> BeginSessionAsync(IMigrationFixture fixture, Ct ct)
    {
        var oracleFixture = (OracleMigrationFixture)fixture;
        var connection = new OracleConnection(oracleFixture.ConnectionString);
        await connection.OpenAsync(ct);
        var transaction = (OracleTransaction)await connection.BeginTransactionAsync(ct);

        long sid;
        await using (var sidCmd = connection.CreateCommand())
        {
            sidCmd.Transaction = transaction;
            sidCmd.CommandText = "SELECT SYS_CONTEXT('USERENV','SID') FROM dual";
            sid = Convert.ToInt64((await sidCmd.ExecuteScalarAsync(ct))!.ToString(), System.Globalization.CultureInfo.InvariantCulture);
        }

        return new OracleConcurrencySession(connection, transaction, oracleFixture.ServerConnectionString, sid);
    }
}

internal sealed class OracleConcurrencySession(
    OracleConnection connection,
    OracleTransaction transaction,
    string serverConnectionString,
    long sid) : IConcurrencySession
{
    private bool _finished;

    public async Task LockEntityRowAsync(EntityType entityType, Guid entityId, Ct ct)
    {
        await using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = """
            SELECT 1 FROM ENTITIES
            WHERE pool_id = :pool_id AND entity_type_id = :entity_type_id AND entity_id = :entity_id
            FOR UPDATE
            """;
        _ = cmd.Parameters.Add(new OracleParameter(":pool_id", PoolId.Default.Value));
        _ = cmd.Parameters.Add(new OracleParameter(":entity_type_id", (int)entityType.Id));
        _ = cmd.Parameters.Add(new OracleParameter(":entity_id", OracleDbType.Raw) { Value = OracleGuidConverter.ToRaw(entityId) });
        var locked = await cmd.ExecuteScalarAsync(ct);
        if (locked is null)
        {
            throw new InvalidOperationException($"No ENTITIES row matched (pool_id={PoolId.Default.Value}, entity_type_id={(int)entityType.Id}, entity_id={entityId}) to lock.");
        }
    }

    public async Task InsertCascadeLinkAsync(LinkDefinition definition, UuidV7 left, UuidV7 right, Ct ct)
    {
        await using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = """
            INSERT INTO ENTITY_LINKS (pool_id, link_type_id, left_entity_type_id, left_entity_id, right_entity_type_id, right_entity_id, restrict_delete)
            VALUES (:pool_id, :link_type_id, :left_entity_type_id, :left_entity_id, :right_entity_type_id, :right_entity_id, 0)
            """;
        _ = cmd.Parameters.Add(new OracleParameter(":pool_id", PoolId.Default.Value));
        _ = cmd.Parameters.Add(new OracleParameter(":link_type_id", (int)definition.Link.Id));
        _ = cmd.Parameters.Add(new OracleParameter(":left_entity_type_id", (int)definition.Left.Id));
        _ = cmd.Parameters.Add(new OracleParameter(":left_entity_id", OracleDbType.Raw) { Value = OracleGuidConverter.ToRaw(left.Value) });
        _ = cmd.Parameters.Add(new OracleParameter(":right_entity_type_id", (int)definition.Right.Id));
        _ = cmd.Parameters.Add(new OracleParameter(":right_entity_id", OracleDbType.Raw) { Value = OracleGuidConverter.ToRaw(right.Value) });
        _ = await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task WaitUntilBlockingAnotherSessionAsync(Ct ct)
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < deadline)
        {
            await using var monitorConnection = new OracleConnection(serverConnectionString);
            await monitorConnection.OpenAsync(ct);
            await using var cmd = monitorConnection.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM v$session WHERE blocking_session = :sid";
            _ = cmd.Parameters.Add(new OracleParameter(":sid", sid));
            var blocked = Convert.ToInt64((await cmd.ExecuteScalarAsync(ct))!.ToString(), System.Globalization.CultureInfo.InvariantCulture);
            if (blocked > 0)
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100), ct);
        }

        throw new TimeoutException("Timed out waiting for another session to be blocked by this session's lock (via v$session.blocking_session); the test's synchronization assumption may no longer hold.");
    }

    public Task PreferOtherSessionAsDeadlockVictimAsync(Ct ct) =>
        // Oracle has no per-session deadlock priority knob. Its deadlock detection (ORA-00060)
        // signals the error to the session whose lock *request* completes the wait cycle; this
        // schedule arranges for that to be the session's own second LockEntityRowAsync call (the
        // engine's conflicting update, started first, is the one left waiting), so no action is
        // needed here beyond documenting why.
        Task.CompletedTask;

    public async Task<bool> TryLockEntityRowAsync(EntityType entityType, Guid entityId, Ct ct)
    {
        try
        {
            await LockEntityRowAsync(entityType, entityId, ct);
            return true;
        }
        catch (OracleException ex) when (ex.Number == 60)
        {
            // This session's own transaction was chosen as the deadlock victim: Oracle has already
            // implicitly rolled back the statement that lost (not just left the lock request
            // pending), but the transaction itself still needs to be ended explicitly before the
            // connection/transaction objects are reused or disposed, so mark it finished and roll
            // back rather than attempting a COMMIT on a transaction ORA-00060 already broke.
            _finished = true;
            await transaction.RollbackAsync(ct);
            return false;
        }
    }

    public async Task CommitAsync(Ct ct)
    {
        _finished = true;
        await transaction.CommitAsync(ct);
        await transaction.DisposeAsync();
        await connection.DisposeAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (!_finished)
        {
            try
            {
                await transaction.RollbackAsync();
            }
#pragma warning disable CA1031
            catch (Exception)
#pragma warning restore CA1031
            {
                // The connection/transaction may already be broken (e.g. this session lost a
                // deadlock and TryLockEntityRowAsync wasn't the one to observe it); disposal must
                // still proceed either way.
            }
        }

        await transaction.DisposeAsync();
        await connection.DisposeAsync();
    }
}
