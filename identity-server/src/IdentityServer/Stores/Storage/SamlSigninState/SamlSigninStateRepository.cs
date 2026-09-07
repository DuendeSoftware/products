// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.Storage;
using Duende.Storage.Internal;
using Duende.Storage.Internal.Operations;
using Duende.Storage.Internal.Querying.SearchFields;

namespace Duende.IdentityServer.Stores.Storage.SamlSigninState;

#pragma warning disable CA1812 // Avoid uninstantiated internal classes
internal sealed class SamlSigninStateRepository(IStorageFactory storageFactory)
{
    internal async Task<CreateResult> CreateAsync(UuidV7 id, SamlSigninStateDso.V1 dso, Expiration expiration, Ct ct)
    {
        var storage = await storageFactory.GetStorage(ct);
        return await storage.CreateAsync(
            id,
            dso,
            [],
            SearchFieldCollection.Empty,
            expiration,
            [],
            ct);
    }

    internal async Task<(SamlSigninStateDso.V1 Dso, int Version)?> TryReadByIdAsync(Guid id, Ct ct)
    {
        if (!UuidV7.TryValidate(id, out _))
        {
            return null;
        }

        var storage = await storageFactory.GetStorage(ct);
        var result = await storage.TryReadAsync(SamlSigninStateDso.EntityType, UuidV7.From(id), ct);
        return result.Found ? ((SamlSigninStateDso.V1)result.Dso, result.Version.Value) : null;
    }

    internal async Task<UpdateResult> UpdateAsync(
        UuidV7 id,
        SamlSigninStateDso.V1 dso,
        int expectedVersion,
        Expiration expiration,
        Ct ct) =>
        await (await storageFactory.GetStorage(ct)).UpdateAsync(
            id,
            dso,
            expectedVersion,
            [],
            SearchFieldCollection.Empty,
            expiration,
            [],
            ct);

    internal async Task DeleteByIdAsync(Guid id, Ct ct)
    {
        if (!UuidV7.TryValidate(id, out _))
        {
            return;
        }

        await (await storageFactory.GetStorage(ct)).DeleteAsync(
            SamlSigninStateDso.EntityType,
            UuidV7.From(id),
            [],
            ct);
    }
}
