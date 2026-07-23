// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.Storage;
using Duende.Storage.Internal;
using Duende.Storage.Internal.Operations;
using Duende.Storage.Internal.Querying;
using Duende.Storage.Internal.Querying.Expressions;
using Duende.Storage.Internal.Querying.Fields;
using Duende.Storage.Internal.Querying.SearchFields;
using Duende.Storage.Internal.Querying.Sorting;
using Duende.Storage.Pagination;

namespace Duende.IdentityServer.Stores.Storage.PersistedGrants;

#pragma warning disable CA1812 // Avoid uninstantiated internal classes
internal sealed class PersistedGrantRepository(IStoreFactory storeFactory)
{
    internal enum Keys
    {
        GrantKey = 1
    }

    private static class Fields
    {
        public static readonly StringField SubjectId = new("SubjectId");
        public static readonly StringField SessionId = new("SessionId");
        public static readonly StringField ClientId = new("ClientId");
        public static readonly StringField Type = new("Type");
    }

    // === STORE (Upsert) ===

    internal async Task StoreAsync(PersistedGrantDso.V1 dso, int? existingVersion, Ct ct)
    {
        var store = await storeFactory.GetStore(ct);

        if (existingVersion.HasValue)
        {
            await store.UpdateAsync(
                UuidV7.From(dso.Id),
                dso,
                existingVersion.Value,
                [DataStorageKey.Create(PersistedGrantKeyDskV1.Create(dso.Key))],
                BuildSearchFields(dso),
                BuildExpiration(dso),
                [],
                ct);
        }
        else
        {
            var createResult = await store.CreateAsync(
                UuidV7.From(dso.Id),
                dso,
                [DataStorageKey.Create(PersistedGrantKeyDskV1.Create(dso.Key))],
                BuildSearchFields(dso),
                BuildExpiration(dso),
                [],
                ct);

            if (createResult == CreateResult.KeyConflict)
            {
                throw new InvalidOperationException(
                    $"A persisted grant with key '{dso.Key}' already exists. " +
                    "This indicates a concurrent create race condition.");
            }
        }
    }

    // === READ ===

    internal async Task<(PersistedGrantDso.V1 Dso, int Version)?> TryReadByKeyAsync(string key, Ct ct)
    {
        var store = await storeFactory.GetStore(ct);
        var result = await store.TryReadAsync(
            PersistedGrantDso.EntityType,
            DataStorageKey.Create(PersistedGrantKeyDskV1.Create(key)),
            ct);
        return result.Found ? ((PersistedGrantDso.V1)result.Dso, result.Version.Value) : null;
    }

    // === DELETE ===

    internal async Task RemoveByKeyAsync(string key, Ct ct) =>
        await (await storeFactory.GetStore(ct)).DeleteAsync(
            PersistedGrantDso.EntityType,
            DataStorageKey.Create(PersistedGrantKeyDskV1.Create(key)),
            [],
            ct);

    // === QUERY (for GetAllAsync and RemoveAllAsync) ===

    internal async Task<IReadOnlyList<PersistedGrantDso.V1>> QueryByFilterAsync(
        PersistedGrantFilter filter, Ct ct)
    {
        var store = await storeFactory.GetStore(ct);
        var filterExpr = BuildFilter(filter);
        var results = new List<PersistedGrantDso.V1>();
        var pageNumber = 1;

        while (true)
        {
            var range = DataRange.FromPage(pageNumber, 200);
            var result = await store.QueryAsync<PersistedGrantDso.V1>(
                PersistedGrantDso.EntityType, filterExpr, SortParameter.Empty, range, ct);

            results.AddRange(result.Items.Select(e => e.Value));

            if (!result.HasMoreData)
            {
                break;
            }

            pageNumber++;
        }

        return results;
    }

    internal async Task RemoveByFilterAsync(PersistedGrantFilter filter, Ct ct)
    {
        var grants = await QueryByFilterAsync(filter, ct);
        var store = await storeFactory.GetStore(ct);

        foreach (var grant in grants)
        {
            await store.DeleteAsync(
                PersistedGrantDso.EntityType,
                UuidV7.From(grant.Id),
                [],
                ct);
        }
    }

    // === Search Fields ===

    private static SearchFieldCollection BuildSearchFields(PersistedGrantDso.V1 dso)
    {
        var builder = new SearchFieldsBuilder()
            .Add(Fields.ClientId.Path, dso.ClientId)
            .Add(Fields.Type.Path, dso.Type);

        if (dso.SubjectId is not null)
        {
            _ = builder.Add(Fields.SubjectId.Path, dso.SubjectId);
        }

        if (dso.SessionId is not null)
        {
            _ = builder.Add(Fields.SessionId.Path, dso.SessionId);
        }

        return builder.Build();
    }

    // === Filter Builder ===
    // PersistedGrantFilter uses AND semantics across properties.
    // ClientId+ClientIds and Type+Types are merged into OR groups via StringField.In().

    private static IQueryExpression BuildFilter(PersistedGrantFilter filter)
    {
        var expressions = new List<IQueryFilterExpression>();

        // ClientId / ClientIds: merge into single IN expression
        if (filter.ClientIds.Count > 0)
        {
            var clientIds = filter.ClientIds.ToList();
            if (!string.IsNullOrWhiteSpace(filter.ClientId))
            {
                clientIds.Add(filter.ClientId);
            }

            expressions.Add(Fields.ClientId.In(clientIds));
        }
        else if (!string.IsNullOrWhiteSpace(filter.ClientId))
        {
            expressions.Add(Fields.ClientId.Equals(filter.ClientId));
        }

        // SessionId: exact match
        if (!string.IsNullOrWhiteSpace(filter.SessionId))
        {
            expressions.Add(Fields.SessionId.Equals(filter.SessionId));
        }

        // SubjectId: exact match
        if (!string.IsNullOrWhiteSpace(filter.SubjectId))
        {
            expressions.Add(Fields.SubjectId.Equals(filter.SubjectId));
        }

        // Type / Types: merge into single IN expression
        if (filter.Types.Count > 0)
        {
            var types = filter.Types.ToList();
            if (!string.IsNullOrWhiteSpace(filter.Type))
            {
                types.Add(filter.Type);
            }

            expressions.Add(Fields.Type.In(types));
        }
        else if (!string.IsNullOrWhiteSpace(filter.Type))
        {
            expressions.Add(Fields.Type.Equals(filter.Type));
        }

        if (expressions.Count == 0)
        {
            return AllExpression.Instance;
        }

        if (expressions.Count == 1)
        {
            return expressions[0];
        }

        var result = expressions[0];
        for (var i = 1; i < expressions.Count; i++)
        {
            result = result.And(expressions[i]);
        }

        return result;
    }

    // === Expiration ===

    private static Expiration BuildExpiration(PersistedGrantDso.V1 dso) =>
        dso.ExpirationTicks.HasValue
            ? Expiration.AtAbsolute(new DateTimeOffset(
                new DateTime(dso.ExpirationTicks.Value, DateTimeKind.Utc), TimeSpan.Zero))
            : Expiration.NoExpiration;
}
