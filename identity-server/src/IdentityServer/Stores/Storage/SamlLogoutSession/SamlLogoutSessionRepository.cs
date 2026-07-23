// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.Storage;
using Duende.Storage.Internal;
using Duende.Storage.Internal.Operations;
using Duende.Storage.Internal.Querying.SearchFields;

namespace Duende.IdentityServer.Stores.Storage.SamlLogoutSession;

#pragma warning disable CA1812 // Avoid uninstantiated internal classes
internal sealed class SamlLogoutSessionRepository(IStoreFactory storeFactory)
{
    internal enum Keys
    {
        LogoutId = 1,
        RequestId = 2
    }

    internal async Task<CreateResult> CreateAsync(UuidV7 id, SamlLogoutSessionDso.V1 dso, Expiration expiration, Ct ct)
    {
        var store = await storeFactory.GetStore(ct);
        return await store.CreateAsync(
            id,
            dso,
            BuildKeys(dso),
            SearchFieldCollection.Empty,
            expiration,
            [],
            ct);
    }

    internal async Task<(SamlLogoutSessionDso.V1 Dso, int Version, Guid Id)?> TryReadByLogoutIdAsync(string logoutId, Ct ct)
    {
        var store = await storeFactory.GetStore(ct);
        var key = DataStorageKey.Create(SamlLogoutSessionLogoutIdDskV1.Create(logoutId));
        var result = await store.TryReadAsync(SamlLogoutSessionDso.EntityType, key, ct);
        return result.Found ? ((SamlLogoutSessionDso.V1)result.Dso, result.Version.Value, result.Id.Value) : null;
    }

    internal async Task<(SamlLogoutSessionDso.V1 Dso, int Version, Guid Id)?> TryReadByRequestIdAsync(string requestId, Ct ct)
    {
        var store = await storeFactory.GetStore(ct);
        var key = DataStorageKey.Create(SamlLogoutSessionRequestIdDskV1.Create(requestId));
        var result = await store.TryReadAsync(SamlLogoutSessionDso.EntityType, key, ct);
        return result.Found ? ((SamlLogoutSessionDso.V1)result.Dso, result.Version.Value, result.Id.Value) : null;
    }

    internal async Task<UpdateResult> UpdateAsync(
        UuidV7 id,
        SamlLogoutSessionDso.V1 dso,
        int expectedVersion,
        Expiration expiration,
        Ct ct) =>
        await (await storeFactory.GetStore(ct)).UpdateAsync(
            id,
            dso,
            expectedVersion,
            BuildKeys(dso),
            SearchFieldCollection.Empty,
            expiration,
            [],
            ct);

    internal async Task DeleteByLogoutIdAsync(string logoutId, Ct ct)
    {
        var store = await storeFactory.GetStore(ct);
        var key = DataStorageKey.Create(SamlLogoutSessionLogoutIdDskV1.Create(logoutId));
        await store.DeleteAsync(SamlLogoutSessionDso.EntityType, key, [], ct);
    }

    private static DataStorageKey[] BuildKeys(SamlLogoutSessionDso.V1 dso)
    {
        var keys = new DataStorageKey[1 + dso.RequestIds.Count];
        keys[0] = DataStorageKey.Create(SamlLogoutSessionLogoutIdDskV1.Create(dso.LogoutId));
        for (var i = 0; i < dso.RequestIds.Count; i++)
        {
            keys[i + 1] = DataStorageKey.Create(SamlLogoutSessionRequestIdDskV1.Create(dso.RequestIds[i]));
        }

        return keys;
    }
}
