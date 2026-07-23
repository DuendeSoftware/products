// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Models;
using Duende.Storage;
using Duende.Storage.Internal;
using Duende.Storage.Internal.Operations;
using Duende.Storage.Internal.Querying.Fields;
using Duende.Storage.Internal.Querying.SearchFields;
using Duende.Storage.Internal.Querying.Sorting;
using Duende.Storage.Pagination;

namespace Duende.IdentityServer.Stores.Storage.SigningKeys;

#pragma warning disable CA1812 // Avoid uninstantiated internal classes
internal sealed class KeyRepository(IStoreFactory storeFactory)
{
    internal enum Keys
    {
        KeyId = 1
    }

    private static class Fields
    {
        public static readonly StringField Use = new("Use");
    }

    internal async Task<IReadOnlyCollection<SerializedKey>> LoadByUseAsync(string use, Ct ct)
    {
        var store = await storeFactory.GetStore(ct);
        var results = new List<SerializedKey>();
        var page = 1;

        while (true)
        {
            var result = await store.QueryAsync<KeyDso.V1>(
                KeyDso.EntityType,
                Fields.Use.Equals(use),
                SortParameter.Empty,
                DataRange.FromPage(page, DataRangeSize.MaxValue),
                ct);

            results.AddRange(result.Items.Select(e => DsoToModel(e.Value)));

            if (!result.HasMoreData)
            {
                break;
            }

            page++;
        }

        return results;
    }

    internal async Task<CreateResult> CreateAsync(UuidV7 id, SerializedKey key, string use, Ct ct)
    {
        var store = await storeFactory.GetStore(ct);
        var dso = ModelToDso(key, use);
        return await store.CreateAsync(
            id,
            dso,
            [DataStorageKey.Create(KeyIdDskV1.Create(key.Id))],
            BuildSearchFields(use),
            Expiration.NoExpiration,
            [],
            ct);
    }

    internal async Task<DeleteResult> DeleteByIdAsync(string keyId, Ct ct) =>
        await (await storeFactory.GetStore(ct)).DeleteAsync(
            KeyDso.EntityType,
            DataStorageKey.Create(KeyIdDskV1.Create(keyId)),
            [],
            ct);

    private static SerializedKey DsoToModel(KeyDso.V1 dso) =>
        new()
        {
            Id = dso.Id,
            Version = dso.Version,
            Created = new DateTime(dso.CreatedUtcTicks, DateTimeKind.Utc),
            Algorithm = dso.Algorithm,
            IsX509Certificate = dso.IsX509Certificate,
            DataProtected = dso.DataProtected,
            Data = dso.Data
        };

    private static KeyDso.V1 ModelToDso(SerializedKey key, string use) =>
        new()
        {
            Id = key.Id,
            Use = use,
            Version = key.Version,
            CreatedUtcTicks = key.Created.ToUniversalTime().Ticks,
            Algorithm = key.Algorithm,
            IsX509Certificate = key.IsX509Certificate,
            DataProtected = key.DataProtected,
            Data = key.Data
        };

    private static SearchFieldCollection BuildSearchFields(string use) =>
        new SearchFieldsBuilder()
            .Add(Fields.Use.Path, use)
            .Build();
}
