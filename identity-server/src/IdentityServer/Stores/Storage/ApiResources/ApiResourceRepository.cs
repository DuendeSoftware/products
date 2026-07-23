// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Admin.ApiResources;
using Duende.IdentityServer.Stores.Storage.ApiScopes;
using Duende.Storage;
using Duende.Storage.Internal;
using Duende.Storage.Internal.Operations;
using Duende.Storage.Internal.Querying;
using Duende.Storage.Internal.Querying.Expressions;
using Duende.Storage.Internal.Querying.Fields;
using Duende.Storage.Internal.Querying.SearchFields;
using Duende.Storage.Internal.Querying.Sorting;
using Duende.Storage.Pagination;
using Duende.Storage.Querying;
using StorageSortDirection = Duende.Storage.Querying.SortDirection;

namespace Duende.IdentityServer.Stores.Storage.ApiResources;

#pragma warning disable CA1812 // Avoid uninstantiated internal classes
internal sealed class ApiResourceRepository(IStoreFactory storeFactory)
{
    internal enum Keys
    {
        Name = 1
    }

    private static class Fields
    {
        public static readonly StringField Name = new("Name");
        public static readonly BooleanField Enabled = new("Enabled");
        public static readonly StringArrayField Scope = new("Scope");
    }

    internal async Task<CreateResult> CreateAsync(UuidV7 id, ApiResourceDso.V1 dso, Ct ct)
    {
        var store = await storeFactory.GetStore(ct);
        return await store.CreateAsync(
            id,
            dso,
            [DataStorageKey.Create(ApiResourceNameDskV1.Create(dso.Name))],
            BuildSearchFields(dso),
            Expiration.NoExpiration,
            [],
            ct);
    }

    internal async Task<CreateResult> CreateWithScopesAsync(
        UuidV7 id,
        ApiResourceDso.V1 dso,
        IReadOnlyList<ApiScopeReferenceDso.V1> scopeRefs,
        Ct ct)
    {
        // Fall back to simple create when there are no scope relationships
        if (scopeRefs.Count == 0)
        {
            return await CreateAsync(id, dso, ct);
        }

        var store = await storeFactory.GetStore(ct);
        var operations = new List<IStoreOperation>();

        // First operation: create the ApiResource
        operations.Add(CreateOperation.For(
            id,
            dso,
            [DataStorageKey.Create(ApiResourceNameDskV1.Create(dso.Name))],
            BuildSearchFields(dso),
            Expiration.NoExpiration));

        // For each scope, read and update with the new back-reference
        foreach (var scopeRef in scopeRefs)
        {
            var scopeResult = await store.TryReadAsync(ApiScopeDso.EntityType, UuidV7.From(scopeRef.Id), ct);
            if (!scopeResult.Found)
            {
                // Scope was deleted — treat as concurrency conflict
                return CreateResult.KeyConflict;
            }

            var scopeDso = (ApiScopeDso.V1)scopeResult.Dso;
            var updatedReferences = scopeDso.ReferencedByApiResources.ToList();
            updatedReferences.Add(new ApiResourceReferenceDso.V1(id.Value, dso.Name));

            var updatedScopeDso = scopeDso with { ReferencedByApiResources = updatedReferences };

            operations.Add(UpdateOperation.For(
                UuidV7.From(scopeRef.Id),
                updatedScopeDso,
                scopeResult.Version.Value,
                [DataStorageKey.Create(ApiScopeNameDskV1.Create(scopeDso.Name))],
                BuildScopeSearchFields(updatedScopeDso),
                Expiration.NoExpiration));
        }

        var batchResult = await store.ExecuteBatchAsync(operations, [], ct);
        return MapBatchToCreateResult(batchResult);
    }

    internal async Task<(ApiResourceDso.V1 Dso, int Version)?> TryReadByIdAsync(Guid id, Ct ct)
    {
        var store = await storeFactory.GetStore(ct);
        var result = await store.TryReadAsync(ApiResourceDso.EntityType, UuidV7.From(id), ct);
        return result.Found ? ((ApiResourceDso.V1)result.Dso, result.Version.Value) : null;
    }

    internal async Task<(ApiResourceDso.V1 Dso, int Version)?> TryReadByNameAsync(string name, Ct ct)
    {
        var store = await storeFactory.GetStore(ct);
        var result = await store.TryReadAsync(
            ApiResourceDso.EntityType,
            DataStorageKey.Create(ApiResourceNameDskV1.Create(name)),
            ct);
        return result.Found ? ((ApiResourceDso.V1)result.Dso, result.Version.Value) : null;
    }

    internal async Task<UpdateResult> UpdateAsync(UuidV7 id, ApiResourceDso.V1 dso, int expectedVersion, Ct ct) =>
        await (await storeFactory.GetStore(ct)).UpdateAsync(
            id,
            dso,
            expectedVersion,
            [DataStorageKey.Create(ApiResourceNameDskV1.Create(dso.Name))],
            BuildSearchFields(dso),
            expiration: Expiration.NoExpiration,
            outboxEvents: [],
            ct);

    internal async Task<UpdateResult> UpdateWithScopeChangesAsync(
        UuidV7 id,
        ApiResourceDso.V1 dso,
        int expectedVersion,
        IReadOnlyList<ApiScopeReferenceDso.V1> addedScopeRefs,
        IReadOnlyList<Guid> removedScopeIds,
        Ct ct)
    {
        // Fall back to simple update when there are no scope relationship changes
        if (addedScopeRefs.Count == 0 && removedScopeIds.Count == 0)
        {
            return await UpdateAsync(id, dso, expectedVersion, ct);
        }

        var store = await storeFactory.GetStore(ct);
        var operations = new List<IStoreOperation>();

        // First operation: update the ApiResource
        operations.Add(UpdateOperation.For(
            id,
            dso,
            expectedVersion,
            [DataStorageKey.Create(ApiResourceNameDskV1.Create(dso.Name))],
            BuildSearchFields(dso),
            Expiration.NoExpiration));

        // Partition scope changes: scopes in both added and removed lists are "renames"
        // (the resource name changed but the scope is still referenced). Process each scope
        // entity at most once to avoid two UpdateOperations targeting the same entity ID,
        // which would cause a version conflict within the batch.
        var addedById = addedScopeRefs.ToDictionary(s => s.Id);
        var removedIdSet = removedScopeIds.ToHashSet();

        // Scopes that need a back-reference rename (present in both added and removed)
        foreach (var scopeRef in addedScopeRefs.Where(s => removedIdSet.Contains(s.Id)))
        {
            var scopeResult = await store.TryReadAsync(ApiScopeDso.EntityType, UuidV7.From(scopeRef.Id), ct);
            if (!scopeResult.Found)
            {
                // Scope was deleted — treat as concurrency conflict
                return UpdateResult.KeyConflict;
            }

            var scopeDso = (ApiScopeDso.V1)scopeResult.Dso;
            var updatedReferences = scopeDso.ReferencedByApiResources
                .Where(r => r.Id != id.Value)
                .Append(new ApiResourceReferenceDso.V1(id.Value, dso.Name))
                .ToList();

            var updatedScopeDso = scopeDso with { ReferencedByApiResources = updatedReferences };

            operations.Add(UpdateOperation.For(
                UuidV7.From(scopeRef.Id),
                updatedScopeDso,
                scopeResult.Version.Value,
                [DataStorageKey.Create(ApiScopeNameDskV1.Create(scopeDso.Name))],
                BuildScopeSearchFields(updatedScopeDso),
                Expiration.NoExpiration));
        }

        // Scopes added (not in the removed list — genuinely new scope references)
        foreach (var scopeRef in addedScopeRefs.Where(s => !removedIdSet.Contains(s.Id)))
        {
            var scopeResult = await store.TryReadAsync(ApiScopeDso.EntityType, UuidV7.From(scopeRef.Id), ct);
            if (!scopeResult.Found)
            {
                // Scope was deleted — treat as concurrency conflict
                return UpdateResult.KeyConflict;
            }

            var scopeDso = (ApiScopeDso.V1)scopeResult.Dso;
            var updatedReferences = scopeDso.ReferencedByApiResources.ToList();
            updatedReferences.Add(new ApiResourceReferenceDso.V1(id.Value, dso.Name));

            var updatedScopeDso = scopeDso with { ReferencedByApiResources = updatedReferences };

            operations.Add(UpdateOperation.For(
                UuidV7.From(scopeRef.Id),
                updatedScopeDso,
                scopeResult.Version.Value,
                [DataStorageKey.Create(ApiScopeNameDskV1.Create(scopeDso.Name))],
                BuildScopeSearchFields(updatedScopeDso),
                Expiration.NoExpiration));
        }

        // Scopes removed (not in the added list — genuinely dropped scope references)
        foreach (var scopeId in removedScopeIds.Where(sid => !addedById.ContainsKey(sid)))
        {
            var scopeResult = await store.TryReadAsync(ApiScopeDso.EntityType, UuidV7.From(scopeId), ct);
            if (!scopeResult.Found)
            {
                // Scope was already deleted — nothing to update, skip
                continue;
            }

            var scopeDso = (ApiScopeDso.V1)scopeResult.Dso;
            var updatedReferences = scopeDso.ReferencedByApiResources
                .Where(r => r.Id != id.Value)
                .ToList();

            var updatedScopeDso = scopeDso with { ReferencedByApiResources = updatedReferences };

            operations.Add(UpdateOperation.For(
                UuidV7.From(scopeId),
                updatedScopeDso,
                scopeResult.Version.Value,
                [DataStorageKey.Create(ApiScopeNameDskV1.Create(scopeDso.Name))],
                BuildScopeSearchFields(updatedScopeDso),
                Expiration.NoExpiration));
        }

        var batchResult = await store.ExecuteBatchAsync(operations, [], ct);
        return MapBatchToUpdateResult(batchResult);
    }

    internal async Task<DeleteResult> DeleteAsync(Guid id, Ct ct) =>
        await (await storeFactory.GetStore(ct)).DeleteAsync(ApiResourceDso.EntityType, UuidV7.From(id), [], ct);

    internal async Task<DeleteResult> DeleteWithScopeCleanupAsync(
        Guid id,
        IReadOnlyList<ApiScopeReferenceDso.V1> scopeRefs,
        Ct ct)
    {
        // Fall back to simple delete when there are no scope relationships
        if (scopeRefs.Count == 0)
        {
            return await DeleteAsync(id, ct);
        }

        var store = await storeFactory.GetStore(ct);
        var operations = new List<IStoreOperation>();

        // For each scope, read and update to remove the back-reference
        foreach (var scopeRef in scopeRefs)
        {
            var scopeResult = await store.TryReadAsync(ApiScopeDso.EntityType, UuidV7.From(scopeRef.Id), ct);
            if (!scopeResult.Found)
            {
                // Scope was already deleted — nothing to update, skip
                continue;
            }

            var scopeDso = (ApiScopeDso.V1)scopeResult.Dso;
            var updatedReferences = scopeDso.ReferencedByApiResources
                .Where(r => r.Id != id)
                .ToList();

            var updatedScopeDso = scopeDso with { ReferencedByApiResources = updatedReferences };

            operations.Add(UpdateOperation.For(
                UuidV7.From(scopeRef.Id),
                updatedScopeDso,
                scopeResult.Version.Value,
                [DataStorageKey.Create(ApiScopeNameDskV1.Create(scopeDso.Name))],
                BuildScopeSearchFields(updatedScopeDso),
                Expiration.NoExpiration));
        }

        // Last operation: delete the ApiResource by ID
        operations.Add(DeleteOperation.ById(ApiResourceDso.EntityType, UuidV7.From(id)));

        var batchResult = await store.ExecuteBatchAsync(operations, [], ct);
        return MapBatchToDeleteResult(batchResult);
    }

    internal async Task<QueryResult<ApiResourceDso.V1>> QueryAsync(
        QueryRequest<ApiResourceFilter, ApiResourceSortField> request,
        Ct ct)
    {
        var store = await storeFactory.GetStore(ct);
        var filter = BuildFilter(request.Filter?.FilterValue);
        var sort = BuildSort(request.Sort);
        var range = request.Range ?? DataRange.FromPage(1, DataRangeSize.Default);

        var result = await store.QueryAsync<ApiResourceDso.V1>(
            ApiResourceDso.EntityType,
            filter,
            sort,
            range,
            ct);

        return result.ConvertTo(e => e.Value);
    }

    internal async Task<List<ApiResourceDso.V1>> FindByNamesAsync(IEnumerable<string> names, Ct ct)
    {
        var nameList = names.Distinct(StringComparer.Ordinal).ToList();

        if (nameList.Count == 0)
        {
            return [];
        }

        var store = await storeFactory.GetStore(ct);
        var filter = Fields.Name.In(nameList);

        var result = await store.QueryAsync<ApiResourceDso.V1>(
            ApiResourceDso.EntityType,
            filter,
            new SortParameter(Fields.Name),
            // Capped at 1000 — exceeding this many API resources by name lookup is implausible in practice.
            DataRange.FromPage(1, (DataRangeSize)1000),
            ct);

        return result.Items.Select(e => e.Value).ToList();
    }

    internal async Task<List<ApiResourceDso.V1>> FindByScopeNamesAsync(IEnumerable<string> scopeNames, Ct ct)
    {
        var store = await storeFactory.GetStore(ct);
        var scopeList = scopeNames.Distinct(StringComparer.Ordinal).ToList();

        if (scopeList.Count == 0)
        {
            return [];
        }

        IQueryFilterExpression? filter = null;
        foreach (var scope in scopeList)
        {
            var scopeFilter = Fields.Scope.Contains(scope);
            filter = filter is null ? scopeFilter : filter.Or(scopeFilter);
        }

        var result = await store.QueryAsync<ApiResourceDso.V1>(
            ApiResourceDso.EntityType,
            filter!,
            new SortParameter(Fields.Name),
            // Intentional single-page cap. >1000 API resources per scope is implausible in practice.
            // If pagination is ever needed, extract to a loop matching the GetAll* pattern.
            DataRange.FromPage(1, (DataRangeSize)1000),
            ct);

        return result.Items.Select(e => e.Value).ToList();
    }

    private static SearchFieldCollection BuildSearchFields(ApiResourceDso.V1 dso)
    {
        var builder = new SearchFieldsBuilder()
            .Add(Fields.Name.Path, dso.Name)
            .Add(Fields.Enabled.Path, dso.Enabled);

        var scopeIndex = 0;
        foreach (var scope in dso.Scopes)
        {
            _ = builder.Add(Fields.Scope.Path, scopeIndex++, scope.Name);
        }

        return builder.Build();
    }

    private static SearchFieldCollection BuildScopeSearchFields(ApiScopeDso.V1 dso) =>
        new SearchFieldsBuilder()
            .Add(ApiScopeRepository.Fields.Name.Path, dso.Name)
            .Add(ApiScopeRepository.Fields.Enabled.Path, dso.Enabled)
            .Build();

    private static CreateResult MapBatchToCreateResult(BatchResult batchResult)
    {
        if (batchResult.Success)
        {
            return CreateResult.Success;
        }

        var failedOp = batchResult.Results.FirstOrDefault(r => r.Outcome != OperationOutcome.Success);
        if (failedOp is null)
        {
            return CreateResult.KeyConflict;
        }

        return failedOp.Outcome switch
        {
            OperationOutcome.AlreadyExists => CreateResult.AlreadyExists,
            OperationOutcome.KeyConflict => CreateResult.KeyConflict,
            OperationOutcome.DoesNotExist => CreateResult.KeyConflict, // Scope was deleted
            OperationOutcome.UnexpectedVersion => CreateResult.KeyConflict, // Concurrent modification
            _ => CreateResult.KeyConflict
        };
    }

    private static UpdateResult MapBatchToUpdateResult(BatchResult batchResult)
    {
        if (batchResult.Success)
        {
            return UpdateResult.Success;
        }

        var failedOp = batchResult.Results.FirstOrDefault(r => r.Outcome != OperationOutcome.Success);
        if (failedOp is null)
        {
            return UpdateResult.KeyConflict;
        }

        return failedOp.Outcome switch
        {
            OperationOutcome.DoesNotExist => failedOp.Index == 0 ? UpdateResult.DoesNotExist : UpdateResult.KeyConflict,
            OperationOutcome.UnexpectedVersion => failedOp.Index == 0 ? UpdateResult.UnexpectedVersion : UpdateResult.KeyConflict,
            OperationOutcome.KeyConflict => UpdateResult.KeyConflict,
            _ => UpdateResult.KeyConflict
        };
    }

    // DeleteResult only has Success currently
    private static DeleteResult MapBatchToDeleteResult(BatchResult batchResult) => DeleteResult.Success;

    private static IQueryExpression BuildFilter(ApiResourceFilter? filter)
    {
        if (filter is null)
        {
            return AllExpression.Instance;
        }

        var expressions = new List<IQueryFilterExpression>();

        if (!string.IsNullOrWhiteSpace(filter.Name))
        {
            expressions.Add(Fields.Name.Contains(filter.Name));
        }

        if (filter.Enabled.HasValue)
        {
            expressions.Add(Fields.Enabled.Equals(filter.Enabled.Value));
        }

        if (!string.IsNullOrWhiteSpace(filter.Scope))
        {
            expressions.Add(Fields.Scope.Contains(filter.Scope));
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

    private static SortParameter BuildSort(SortBy.SortByField<ApiResourceSortField>? sort)
    {
        if (sort is null)
        {
            return new SortParameter(Fields.Name);
        }

        Field field = sort.Field switch
        {
            ApiResourceSortField.Name => Fields.Name,
            ApiResourceSortField.Enabled => Fields.Enabled,
            _ => throw new ArgumentOutOfRangeException(nameof(sort), sort.Field, "Unknown sort field")
        };

        var direction = sort.Direction switch
        {
            SortDirection.Ascending => StorageSortDirection.Ascending,
            SortDirection.Descending => StorageSortDirection.Descending,
            _ => throw new ArgumentOutOfRangeException(nameof(sort), sort.Direction, "Unknown sort direction")
        };

        return new SortParameter(field, direction);
    }
}
