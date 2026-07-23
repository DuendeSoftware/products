// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.Storage;
using Duende.Storage.Internal;
using Duende.Storage.Internal.Operations;
using Duende.Storage.Internal.Querying.SearchFields;

namespace Duende.IdentityServer.Stores.Storage.PushedAuthorization;

#pragma warning disable CA1812 // Avoid uninstantiated internal classes
internal sealed class PushedAuthorizationRepository(IStoreFactory storeFactory)
{
    internal enum Keys
    {
        ReferenceValueHash = 1
    }

    internal async Task<CreateResult> CreateAsync(
        UuidV7 id,
        PushedAuthorizationDso.V1 dso,
        Expiration expiration,
        Ct ct)
    {
        var store = await storeFactory.GetStore(ct);
        return await store.CreateAsync(
            id,
            dso,
            [DataStorageKey.Create(ReferenceValueHashDskV1.Create(dso.ReferenceValueHash))],
            SearchFieldCollection.Empty,
            expiration,
            [],
            ct);
    }

    internal async Task<PushedAuthorizationDso.V1?> TryReadByHashAsync(string referenceValueHash, Ct ct)
    {
        var store = await storeFactory.GetStore(ct);
        var result = await store.TryReadAsync(
            PushedAuthorizationDso.EntityType,
            DataStorageKey.Create(ReferenceValueHashDskV1.Create(referenceValueHash)),
            ct);
        return result.Found ? (PushedAuthorizationDso.V1)result.Dso : null;
    }

    internal async Task<DeleteResult> DeleteByHashAsync(string referenceValueHash, Ct ct) =>
        await (await storeFactory.GetStore(ct)).DeleteAsync(
            PushedAuthorizationDso.EntityType,
            DataStorageKey.Create(ReferenceValueHashDskV1.Create(referenceValueHash)),
            [],
            ct);
}
