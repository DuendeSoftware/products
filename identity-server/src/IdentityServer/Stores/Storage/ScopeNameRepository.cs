// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.Storage;
using Duende.Storage.Internal;
using Duende.Storage.Internal.Operations;
using Duende.Storage.Internal.Querying.SearchFields;

namespace Duende.IdentityServer.Stores.Storage;

/// <summary>
/// The outcome of a batch operation that creates, renames or deletes an API scope or identity resource
/// together with its <see cref="ScopeNameDso"/> name guard.
/// </summary>
internal enum ScopeNameBatchOutcomeKind
{
    Success,

    /// <summary>
    /// Create or rename failed: another entity of the same kind already uses the name.
    /// </summary>
    AlreadyExists,

    /// <summary>
    /// Create or rename failed: an entity of the other kind (API scope vs. identity resource) already
    /// uses the name.
    /// </summary>
    NameTakenByOtherKind,

    /// <summary>
    /// Update or delete failed: the entity does not exist.
    /// </summary>
    DoesNotExist,

    /// <summary>
    /// Update failed: the entity version did not match the expected version.
    /// </summary>
    UnexpectedVersion
}

internal readonly record struct ScopeNameBatchOutcome(ScopeNameBatchOutcomeKind Kind)
{
    public static readonly ScopeNameBatchOutcome Success = new(ScopeNameBatchOutcomeKind.Success);
}

/// <summary>
/// Builds the <see cref="ScopeNameDso"/> guard operations that <see cref="ApiScopes.ApiScopeRepository"/>
/// and <see cref="IdentityResources.IdentityResourceRepository"/> add to their own batches, and maps the
/// resulting <see cref="BatchResult"/> to a <see cref="ScopeNameBatchOutcome"/>.
/// </summary>
internal static class ScopeNameRepository
{
    internal enum Keys
    {
        Name = 1
    }

    /// <summary>
    /// A create operation for the name guard. Its ID is the same as the ID of the API scope or identity
    /// resource it guards; its unique key is the name, shared by both kinds.
    /// </summary>
    internal static IStorageOperation CreateGuardOperation(UuidV7 id, string name) =>
        CreateOperation.For(
            id,
            new ScopeNameDso.V1 { Name = name },
            [DataStorageKey.Create(ScopeNameGuardDskV1.Create(name))],
            SearchFieldCollection.Empty,
            Expiration.NoExpiration);

    /// <summary>
    /// A delete operation for the name guard with the given (old) name. Storage's delete is idempotent
    /// for a missing key, so this tolerates rows saved before the guard existed.
    /// </summary>
    internal static IStorageOperation DeleteGuardOperation(string name) =>
        DeleteOperation.ByKey(ScopeNameDso.EntityType, DataStorageKey.Create(ScopeNameGuardDskV1.Create(name)));

    /// <summary>
    /// A delete operation for the name guard by ID. Storage's delete is idempotent for a missing ID, so
    /// this tolerates rows saved before the guard existed.
    /// </summary>
    internal static IStorageOperation DeleteGuardOperation(UuidV7 id) =>
        DeleteOperation.ById(ScopeNameDso.EntityType, id);

    /// <summary>
    /// Maps a batch result where index 0 is the Create/Update operation for the entity itself, and (for
    /// create/rename) one or more guard operations follow it: a create (on create), or a delete-by-id
    /// followed by a create (on rename).
    /// </summary>
    internal static ScopeNameBatchOutcome MapBatchOutcome(IReadOnlyList<IStorageOperation> operations, BatchResult batchResult)
    {
        if (batchResult.Success)
        {
            return ScopeNameBatchOutcome.Success;
        }

        var failure = batchResult.Results.First(r => r.Outcome is not OperationOutcome.Success);

        if (failure.Index == 0)
        {
            return failure.Outcome switch
            {
                OperationOutcome.AlreadyExists or OperationOutcome.KeyConflict =>
                    new ScopeNameBatchOutcome(ScopeNameBatchOutcomeKind.AlreadyExists),
                OperationOutcome.DoesNotExist => new ScopeNameBatchOutcome(ScopeNameBatchOutcomeKind.DoesNotExist),
                OperationOutcome.UnexpectedVersion => new ScopeNameBatchOutcome(ScopeNameBatchOutcomeKind.UnexpectedVersion),
                _ => throw new InvalidOperationException($"Unexpected OperationOutcome: {failure.Outcome}")
            };
        }

        if (operations[failure.Index] is DeleteOperation)
        {
            // The guard delete-by-id is idempotent for a missing guard (returns Success), so the only
            // documented failure left for a delete is StillLinked. Nothing ever links to a ScopeNameDso
            // guard, so this should not happen in practice; map it defensively instead of throwing.
            return failure.Outcome switch
            {
                OperationOutcome.StillLinked => new ScopeNameBatchOutcome(ScopeNameBatchOutcomeKind.AlreadyExists),
                _ => throw new InvalidOperationException($"Unexpected OperationOutcome: {failure.Outcome}")
            };
        }

        // The guard create: its own-kind name key (index 0) already succeeded, so a KeyConflict here
        // can only be the other kind holding the name. AlreadyExists (the same ID already has a guard)
        // should not happen either, because this ID's prior guard, if any, was just deleted by ID earlier
        // in this same batch. It is mapped rather than thrown, since it names the same kind of conflict
        // that the other-kind name read already guards against.
        return failure.Outcome switch
        {
            OperationOutcome.KeyConflict => new ScopeNameBatchOutcome(ScopeNameBatchOutcomeKind.NameTakenByOtherKind),
            OperationOutcome.AlreadyExists => new ScopeNameBatchOutcome(ScopeNameBatchOutcomeKind.AlreadyExists),
            _ => throw new InvalidOperationException($"Unexpected OperationOutcome: {failure.Outcome}")
        };
    }
}
