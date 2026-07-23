// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.Storage;
using Duende.Storage.Internal;
using Duende.Storage.Internal.Operations;
using Duende.Storage.Internal.Querying.SearchFields;

namespace Duende.IdentityServer.Stores.Storage.DeviceFlow;

#pragma warning disable CA1812 // Avoid uninstantiated internal classes
internal sealed class DeviceFlowRepository(IStoreFactory storeFactory)
{
    internal enum Keys
    {
        DeviceCode = 1,
        UserCode = 2
    }

    internal async Task<CreateResult> CreateAsync(
        UuidV7 id,
        DeviceFlowDso.V1 dso,
        Expiration expiration,
        Ct ct)
    {
        var store = await storeFactory.GetStore(ct);
        return await store.CreateAsync(
            id,
            dso,
            [
                DataStorageKey.Create(DeviceCodeDskV1.Create(dso.DeviceCode)),
                DataStorageKey.Create(UserCodeDskV1.Create(dso.UserCode))
            ],
            SearchFieldCollection.Empty,
            expiration,
            [],
            ct);
    }

    internal async Task<(DeviceFlowDso.V1 Dso, UuidV7 Id, int Version)?>
        TryReadByDeviceCodeAsync(string deviceCode, Ct ct)
    {
        var store = await storeFactory.GetStore(ct);
        var result = await store.TryReadAsync(
            DeviceFlowDso.EntityType,
            DataStorageKey.Create(DeviceCodeDskV1.Create(deviceCode)),
            ct);
        return result.Found
            ? ((DeviceFlowDso.V1)result.Dso, UuidV7.From(result.Id.Value), result.Version.Value)
            : null;
    }

    internal async Task<(DeviceFlowDso.V1 Dso, UuidV7 Id, int Version)?>
        TryReadByUserCodeAsync(string userCode, Ct ct)
    {
        var store = await storeFactory.GetStore(ct);
        var result = await store.TryReadAsync(
            DeviceFlowDso.EntityType,
            DataStorageKey.Create(UserCodeDskV1.Create(userCode)),
            ct);
        return result.Found
            ? ((DeviceFlowDso.V1)result.Dso, UuidV7.From(result.Id.Value), result.Version.Value)
            : null;
    }

    internal async Task<UpdateResult> UpdateAsync(
        UuidV7 id,
        DeviceFlowDso.V1 dso,
        int expectedVersion,
        Ct ct) =>
        await (await storeFactory.GetStore(ct)).UpdateAsync(
            id,
            dso,
            expectedVersion,
            [
                DataStorageKey.Create(DeviceCodeDskV1.Create(dso.DeviceCode)),
                DataStorageKey.Create(UserCodeDskV1.Create(dso.UserCode))
            ],
            SearchFieldCollection.Empty,
            expiration: null, // preserve existing TTL
            outboxEvents: [],
            ct);

    internal async Task<DeleteResult> DeleteByDeviceCodeAsync(string deviceCode, Ct ct) =>
        await (await storeFactory.GetStore(ct)).DeleteAsync(
            DeviceFlowDso.EntityType,
            DataStorageKey.Create(DeviceCodeDskV1.Create(deviceCode)),
            [],
            ct);
}
