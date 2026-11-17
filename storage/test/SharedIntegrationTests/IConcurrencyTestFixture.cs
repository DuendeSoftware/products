// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal;

namespace Duende.Storage.IntegrationTests;

/// <summary>
/// Per-engine abstraction for the raw, second connection/transaction that
/// <see cref="StoreLinkDeterministicConcurrencyTests"/> needs alongside the engine under test, to make a
/// row lock or a deadlock deterministic instead of timing-based.
/// </summary>
public interface IConcurrencyTestFixture
{
    /// <summary>
    /// Opens a second connection and starts a transaction against the same database/schema as
    /// <paramref name="fixture"/>, independent of the engine's own connection.
    /// </summary>
    Task<IConcurrencySession> BeginSessionAsync(IMigrationFixture fixture, Ct ct);
}

/// <summary>
/// A raw, engine-specific second connection/transaction used to make the engine's own connection block
/// deterministically, and to control which side a deadlock picks as its victim.
/// </summary>
public interface IConcurrencySession : IAsyncDisposable
{
    /// <summary>
    /// Locks the given entity's row (<c>entities</c>) without modifying it (e.g. <c>SELECT ... FOR
    /// UPDATE</c> / <c>WITH (UPDLOCK, ROWLOCK, HOLDLOCK)</c>), on this session's transaction.
    /// </summary>
    Task LockEntityRowAsync(EntityType entityType, Guid entityId, Ct ct);

    /// <summary>
    /// Inserts a non-restricted (Cascade) <c>entity_links</c> row directly, on this session's
    /// transaction, without going through <see cref="IPartitionedStorage.LinkAsync"/>.
    /// </summary>
    Task InsertCascadeLinkAsync(LinkDefinition definition, UuidV7 left, UuidV7 right, Ct ct);

    /// <summary>
    /// Bounded, polling wait for evidence (via the engine's system catalog/DMVs) that some other
    /// session is blocked waiting on a lock this session holds. Throws <see cref="TimeoutException"/>
    /// if no such evidence appears in time, rather than guessing a delay.
    /// </summary>
    Task WaitUntilBlockingAnotherSessionAsync(Ct ct);

    /// <summary>
    /// Arranges, as far as this engine allows, for the *other* session involved in a future lock
    /// conflict with this one to be chosen as the deadlock victim (SQL Server: raises this session's
    /// deadlock priority; PostgreSQL: raises this session's <c>deadlock_timeout</c> so the engine's
    /// backend, which started waiting first, detects the cycle on its own shorter timeout; Oracle: a
    /// documented no-op — Oracle always signals the session whose statement completes the wait cycle,
    /// which this schedule arranges to be the engine's).
    /// </summary>
    Task PreferOtherSessionAsDeadlockVictimAsync(Ct ct);

    /// <summary>
    /// Requests the same kind of row lock as <see cref="LockEntityRowAsync"/> for a second entity,
    /// used as the second half of a deadlock schedule. Returns <c>true</c> if the lock was granted
    /// without this session's own transaction being aborted by the engine's database as a deadlock
    /// victim; <c>false</c> if it was (which a correct schedule must never see).
    /// </summary>
    Task<bool> TryLockEntityRowAsync(EntityType entityType, Guid entityId, Ct ct);

    /// <summary>Commits this session's transaction, releasing every lock it holds.</summary>
    Task CommitAsync(Ct ct);
}
