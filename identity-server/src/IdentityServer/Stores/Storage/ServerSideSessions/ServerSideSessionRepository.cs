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
using Duende.Storage.Querying;
using IdentityServerModels = Duende.IdentityServer.Models;

namespace Duende.IdentityServer.Stores.Storage.ServerSideSessions;

#pragma warning disable CA1812 // Avoid uninstantiated internal classes
internal sealed class ServerSideSessionRepository(IStoreFactory storeFactory, TimeProvider timeProvider)
{
    internal enum Keys
    {
        SessionKey = 1
    }

    private static class Fields
    {
        public static readonly StringField SubjectId = new("SubjectId");
        public static readonly StringField SessionId = new("SessionId");
        public static readonly StringField DisplayName = new("DisplayName");
        public static readonly DateTimeField Expires = new("Expires");
    }

    internal async Task<CreateResult> CreateAsync(IdentityServerModels.ServerSideSession session, Ct ct)
    {
        var store = await storeFactory.GetStore(ct);
        var id = UuidV7.New();
        var dso = ModelToDso(session);
        return await store.CreateAsync(
            id,
            dso,
            [DataStorageKey.Create(SessionKeyDskV1.Create(session.Key))],
            BuildSearchFields(session),
            BuildExpiration(session),
            [],
            ct);
    }

    internal async Task<(Guid Id, int Version, ServerSideSessionDso.V1 Dso)?> TryReadByKeyAsync(string key, Ct ct)
    {
        var store = await storeFactory.GetStore(ct);
        var result = await store.TryReadAsync(
            ServerSideSessionDso.EntityType,
            DataStorageKey.Create(SessionKeyDskV1.Create(key)),
            ct);
        return result.Found ? (result.Id.Value, result.Version.Value, (ServerSideSessionDso.V1)result.Dso) : null;
    }

    internal async Task<UpdateResult> UpdateAsync(Guid id, int version, IdentityServerModels.ServerSideSession session, Ct ct) =>
        await (await storeFactory.GetStore(ct)).UpdateAsync(
            UuidV7.From(id),
            ModelToDso(session),
            version,
            [DataStorageKey.Create(SessionKeyDskV1.Create(session.Key))],
            BuildSearchFields(session),
            BuildExpiration(session),
            [],
            ct);

    internal async Task<DeleteResult> DeleteByKeyAsync(string key, Ct ct) =>
        await (await storeFactory.GetStore(ct)).DeleteAsync(
            ServerSideSessionDso.EntityType,
            DataStorageKey.Create(SessionKeyDskV1.Create(key)),
            [],
            ct);

    /// <summary>
    /// Queries all sessions matching the filter, paging internally to collect all results.
    /// Note: Callers that query then delete (DeleteSessionsAsync, GetAndRemoveExpiredSessionsAsync)
    /// are not atomic — concurrent operations may modify results between query and delete.
    /// This matches the EF implementation behavior and is acceptable for background cleanup.
    /// </summary>
    internal async Task<IReadOnlyList<(Guid Id, int Version, ServerSideSessionDso.V1 Dso)>> QueryByFilterAsync(
        SessionFilter filter,
        Ct ct)
    {
        var store = await storeFactory.GetStore(ct);
        var filterExpr = BuildSessionFilterExpression(filter);
        var results = new List<(Guid, int, ServerSideSessionDso.V1)>();
        var page = 1;

        while (true)
        {
            var result = await store.QueryAsync<ServerSideSessionDso.V1>(
                ServerSideSessionDso.EntityType,
                filterExpr,
                SortParameter.Empty,
                DataRange.FromPage(page, DataRangeSize.MaxValue),
                ct);

            results.AddRange(result.Items.Select(e => (e.Id, e.Version, e.Value)));

            if (!result.HasMoreData)
            {
                break;
            }

            page++;
        }

        return results;
    }

    internal async Task DeleteByIdsAsync(IReadOnlyList<Guid> ids, Ct ct)
    {
        if (ids.Count == 0)
        {
            return;
        }

        var store = await storeFactory.GetStore(ct);
        var operations = ids
            .Select(id => (IStoreOperation)DeleteOperation.ById(ServerSideSessionDso.EntityType, UuidV7.From(id)))
            .ToArray();

        await store.ExecuteBatchAsync(operations, [], ct);
    }

    internal async Task<IReadOnlyList<(Guid Id, ServerSideSessionDso.V1 Dso)>> QueryExpiredAsync(int count, Ct ct)
    {
        if (count <= 0)
        {
            return [];
        }

        var store = await storeFactory.GetStore(ct);
        var size = Math.Min(count, DataRangeSize.MaxValue);
        var result = await store.QueryAsync<ServerSideSessionDso.V1>(
            ServerSideSessionDso.EntityType,
            Fields.Expires.LessThan(timeProvider.GetUtcNow()),
            new SortParameter(SystemFields.CreatedAtField),
            DataRange.FromPage(1, size),
            ct);

        return result.Items.Select(e => (e.Id, e.Value)).ToArray();
    }

    /// <summary>
    /// Queries sessions using the store's native continuation-token pagination.
    /// When a ResultsToken is present, it is decoded as "nextToken|previousToken" and the
    /// appropriate token is used based on RequestPriorResults. When no token is present,
    /// starts from the beginning. Sort order is determined by the store (creation order via ID).
    /// </summary>
    internal async Task<QueryResult<ServerSideSessionDso.V1>> QueryPagedAsync(
        SessionQuery? filter,
        Ct ct)
    {
        filter ??= new SessionQuery();

        var countRequested = filter.CountRequested > 0 ? filter.CountRequested : 25;
        var pageSize = Math.Min(countRequested, DataRangeSize.MaxValue);

        var store = await storeFactory.GetStore(ct);
        var filterExpr = BuildSessionQueryExpression(filter);

        // Decode the ResultsToken: format is "nextToken|previousToken"
        ContinuationToken? token = null;
        if (filter.ResultsToken != null)
        {
            var parts = filter.ResultsToken.Split('|', 2);
            var nextPart = parts[0];
            var prevPart = parts.Length > 1 ? parts[1] : string.Empty;

            var selectedToken = filter.RequestPriorResults ? prevPart : nextPart;
            if (!string.IsNullOrEmpty(selectedToken))
            {
                token = (ContinuationToken)selectedToken;
            }
        }

        var dataRange = DataRange.FromContinuationToken(token, pageSize);

        var sort = filter.RequestPriorResults
            ? new SortParameter(SystemFields.CreatedAtField, SortDirection.Descending)
            : new SortParameter(SystemFields.CreatedAtField);

        var result = await store.QueryAsync<ServerSideSessionDso.V1>(
            ServerSideSessionDso.EntityType,
            filterExpr,
            sort,
            dataRange,
            ct);

        var converted = result.ConvertTo(e => e.Value);

        // When navigating backward, the query used a reversed sort order.
        // We need to normalize the result so that the caller always sees:
        // - Items in forward (ascending) order
        // - NextToken = token to get the next forward page
        // - PreviousToken = token to get the previous (backward) page
        if (filter.RequestPriorResults)
        {
            converted = converted with
            {
                Items = converted.Items.Reverse().ToArray(),
                NextToken = result.PreviousToken,
                PreviousToken = result.NextToken
            };
        }

        return converted;
    }

    internal static IdentityServerModels.ServerSideSession DsoToModel(ServerSideSessionDso.V1 dso) =>
        new()
        {
            Key = dso.Key,
            Scheme = dso.Scheme,
            SubjectId = dso.SubjectId,
            SessionId = dso.SessionId,
            DisplayName = dso.DisplayName,
            Created = new DateTime(dso.CreatedUtcTicks, DateTimeKind.Utc),
            Renewed = new DateTime(dso.RenewedUtcTicks, DateTimeKind.Utc),
            Expires = dso.ExpiresUtcTicks.HasValue
                ? new DateTime(dso.ExpiresUtcTicks.Value, DateTimeKind.Utc)
                : null,
            Ticket = dso.Ticket
        };

    private static ServerSideSessionDso.V1 ModelToDso(IdentityServerModels.ServerSideSession session) =>
        new()
        {
            Key = session.Key,
            Scheme = session.Scheme,
            SubjectId = session.SubjectId,
            SessionId = session.SessionId,
            DisplayName = session.DisplayName,
            CreatedUtcTicks = session.Created.Ticks,
            RenewedUtcTicks = session.Renewed.Ticks,
            ExpiresUtcTicks = session.Expires?.Ticks,
            Ticket = session.Ticket
        };

    private static SearchFieldCollection BuildSearchFields(IdentityServerModels.ServerSideSession session)
    {
        var builder = new SearchFieldsBuilder()
            .Add(Fields.SubjectId.Path, session.SubjectId)
            .Add(Fields.SessionId.Path, session.SessionId);

        if (session.DisplayName is not null)
        {
            _ = builder.Add(Fields.DisplayName.Path, session.DisplayName);
        }

        if (session.Expires.HasValue)
        {
            _ = builder.Add(Fields.Expires.Path,
                new DateTimeOffset(DateTime.SpecifyKind(session.Expires.Value, DateTimeKind.Utc)));
        }

        return builder.Build();
    }

    private static Expiration BuildExpiration(IdentityServerModels.ServerSideSession session) =>
        session.Expires.HasValue
            ? Expiration.AtAbsolute(new DateTimeOffset(DateTime.SpecifyKind(session.Expires.Value, DateTimeKind.Utc)))
            : Expiration.NoExpiration;

    private static IQueryExpression BuildSessionFilterExpression(SessionFilter filter)
    {
        var expressions = new List<IQueryFilterExpression>();

        if (!string.IsNullOrWhiteSpace(filter.SubjectId))
        {
            expressions.Add(Fields.SubjectId.Equals(filter.SubjectId));
        }

        if (!string.IsNullOrWhiteSpace(filter.SessionId))
        {
            expressions.Add(Fields.SessionId.Equals(filter.SessionId));
        }

        if (expressions.Count == 0)
        {
            return AllExpression.Instance;
        }

        if (expressions.Count == 1)
        {
            return expressions[0];
        }

        return expressions[0].And(expressions[1]);
    }

    private static IQueryExpression BuildSessionQueryExpression(SessionQuery filter)
    {
        var expressions = new List<IQueryFilterExpression>();

        if (!string.IsNullOrWhiteSpace(filter.SubjectId))
        {
            expressions.Add(Fields.SubjectId.Contains(filter.SubjectId));
        }

        if (!string.IsNullOrWhiteSpace(filter.SessionId))
        {
            expressions.Add(Fields.SessionId.Contains(filter.SessionId));
        }

        if (!string.IsNullOrWhiteSpace(filter.DisplayName))
        {
            expressions.Add(Fields.DisplayName.Contains(filter.DisplayName));
        }

        if (expressions.Count == 0)
        {
            return AllExpression.Instance;
        }

        var result = expressions[0];
        for (var i = 1; i < expressions.Count; i++)
        {
            result = result.And(expressions[i]);
        }

        return result;
    }
}
