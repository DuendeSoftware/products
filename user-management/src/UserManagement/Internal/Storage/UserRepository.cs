// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage;
using Duende.Storage.Internal;
using Duende.Storage.Internal.Operations;

namespace Duende.UserManagement.Internal.Storage;

#pragma warning disable CA1812 // Avoid uninstantiated internal classes
internal sealed class UserRepository(IStoreFactory storeFactory)
{
    internal enum Keys
    {
        SubjectId = 1
    }

    internal async Task<CreateResult> CreateAsync(UserSubjectId subjectId, Ct ct)
    {
        var store = storeFactory.GetStore();
        var id = UuidV7.New();
        return await store.CreateAsync(
            id,
            new UserDso.V1(id.Value, subjectId.Value, []),
            [DataStorageKey.Create(UserSubjectIdDskV1.Create(subjectId))],
            [],
            Expiration.NoExpiration,
            [],
            ct);
    }

    internal async Task<(UserDso.V1 User, int Version)?> TryReadAsync(UserSubjectId subjectId, Ct ct)
    {
        var store = storeFactory.GetStore();
        var result = await store.TryReadAsync(UserDso.EntityType, DataStorageKey.Create(UserSubjectIdDskV1.Create(subjectId)), ct);
        return result.Found ? ((UserDso.V1)result.Dso, result.Version.Value) : null;
    }

    internal async Task<UpdateResult> UpdateAsync(UserDso.V1 user, int expectedVersion, Ct ct) =>
        await storeFactory.GetStore().UpdateAsync(
            UuidV7.From(user.Id),
            user,
            expectedVersion,
            [DataStorageKey.Create(UserSubjectIdDskV1.Create(UserSubjectId.Load(user.SubjectId)))],
            [],
            expiration: Expiration.NoExpiration,
            [],
            ct);

    internal static CreateOperation CreateBatchOperation(UserSubjectId subjectId, IReadOnlyList<UserDso.AspectRef> aspects)
    {
        var id = UuidV7.New();
        return CreateBatchOperation(id, subjectId, aspects);
    }

    internal static CreateOperation CreateBatchOperation(UuidV7 id, UserSubjectId subjectId, IReadOnlyList<UserDso.AspectRef> aspects) =>
        CreateOperation.For(
            id,
            new UserDso.V1(id.Value, subjectId.Value, aspects),
            [DataStorageKey.Create(UserSubjectIdDskV1.Create(subjectId))],
            [],
            Expiration.NoExpiration);

    internal static UpdateOperation UpdateBatchOperation(UserDso.V1 user, int expectedVersion) =>
        UpdateOperation.For(
            UuidV7.From(user.Id),
            user,
            expectedVersion,
            [DataStorageKey.Create(UserSubjectIdDskV1.Create(UserSubjectId.Load(user.SubjectId)))],
            [],
            Expiration.NoExpiration);

    internal async Task<long> GetCountAsync(Ct ct)
    {
        var store = storeFactory.GetStore();
        return await store.CountAsync(UserDso.EntityType, null, ct);
    }

    internal static DeleteOperation DeleteBatchOperation(UserSubjectId subjectId) =>
        DeleteOperation.ByKey(UserDso.EntityType, DataStorageKey.Create(UserSubjectIdDskV1.Create(subjectId)));

    /// <summary>
    /// Resolves a list of <see cref="UserSubjectId"/> values to their UserDso UUIDs.
    /// Returns a dictionary of successfully resolved mappings and a list of subject IDs
    /// that could not be found.
    /// </summary>
    internal async Task<(Dictionary<UserSubjectId, UuidV7> Resolved, List<UserSubjectId> NotFound)>
        ResolveUserUuidsAsync(IReadOnlyList<UserSubjectId> subjectIds, Ct ct)
    {
        var store = storeFactory.GetStore();
        var resolved = new Dictionary<UserSubjectId, UuidV7>(subjectIds.Count);
        var notFound = new List<UserSubjectId>();

        foreach (var subjectId in subjectIds)
        {
            var result = await store.TryReadAsync(
                UserDso.EntityType,
                DataStorageKey.Create(UserSubjectIdDskV1.Create(subjectId)),
                ct);

            if (result.Found)
            {
                resolved[subjectId] = UuidV7.From(result.Id.Value);
            }
            else
            {
                notFound.Add(subjectId);
            }
        }

        return (resolved, notFound);
    }

    internal static UserDso.V1 AddOrUpdateAspectRef(UserDso.V1 user, UserDso.AspectRef aspectRef)
    {
        var aspects = new List<UserDso.AspectRef>(user.Aspects.Count + 1);
        var updated = false;

        foreach (var existing in user.Aspects)
        {
            if (existing.AspectEntityTypeId == aspectRef.AspectEntityTypeId)
            {
                aspects.Add(aspectRef);
                updated = true;
            }
            else
            {
                aspects.Add(existing);
            }
        }

        if (!updated)
        {
            aspects.Add(aspectRef);
        }

        return user with { Aspects = aspects };
    }
}
