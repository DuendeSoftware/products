// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Models;
using Duende.Storage;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Stores.Storage.PersistedGrants;

#pragma warning disable CA1812 // Avoid uninstantiated internal classes
internal sealed class PersistedGrantStore(
    PersistedGrantRepository repository,
    ILogger<PersistedGrantStore> logger) : IPersistedGrantStore
{
    /// <inheritdoc/>
    public async Task StoreAsync(PersistedGrant grant, Ct ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("PersistedGrantStore.Store");

        var existing = await repository.TryReadByKeyAsync(grant.Key, ct);

        if (existing is not null)
        {
            logger.UpdatingPersistedGrant(LogLevel.Debug, grant.Key);
        }
        else
        {
            logger.CreatingPersistedGrant(LogLevel.Debug, grant.Key);
        }

        var id = existing?.Dso.Id ?? UuidV7.New().Value;
        var dso = MapToDso(id, grant);

        await repository.StoreAsync(dso, existing?.Version, ct);
    }

    /// <inheritdoc/>
    public async Task<PersistedGrant?> GetAsync(string key, Ct ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("PersistedGrantStore.Get");

        var result = await repository.TryReadByKeyAsync(key, ct);
        var model = result is null ? null : MapToModel(result.Value.Dso);

        logger.PersistedGrantFound(LogLevel.Debug, key, model != null);

        return model;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<PersistedGrant>> GetAllAsync(PersistedGrantFilter filter, Ct ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("PersistedGrantStore.GetAll");

        filter.Validate();
        var results = await repository.QueryByFilterAsync(filter, ct);
        var grants = results.Select(MapToModel).ToList();

        logger.PersistedGrantsFound(LogLevel.Debug, grants.Count, filter);

        return grants;
    }

    /// <inheritdoc/>
    public async Task RemoveAsync(string key, Ct ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("PersistedGrantStore.Remove");

        logger.RemovingPersistedGrant(LogLevel.Debug, key);
        await repository.RemoveByKeyAsync(key, ct);
    }

    /// <inheritdoc/>
    public async Task RemoveAllAsync(PersistedGrantFilter filter, Ct ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("PersistedGrantStore.RemoveAll");

        filter.Validate();
        logger.RemovingPersistedGrants(LogLevel.Debug, filter);
        await repository.RemoveByFilterAsync(filter, ct);
    }

    private static PersistedGrantDso.V1 MapToDso(Guid id, PersistedGrant grant) => new()
    {
        Id = id,
        Key = grant.Key,
        Type = grant.Type,
        SubjectId = grant.SubjectId, // May be null (client credentials flows)
        SessionId = grant.SessionId,
        ClientId = grant.ClientId,
        Description = grant.Description,
        CreationTimeTicks = grant.CreationTime.Ticks,
        ExpirationTicks = grant.Expiration?.Ticks,
        ConsumedTimeTicks = grant.ConsumedTime?.Ticks,
        Data = grant.Data
    };

    private static PersistedGrant MapToModel(PersistedGrantDso.V1 dso) => new()
    {
        Key = dso.Key,
        Type = dso.Type,
        SubjectId = dso.SubjectId!,
        SessionId = dso.SessionId,
        ClientId = dso.ClientId,
        Description = dso.Description,
        CreationTime = new DateTime(dso.CreationTimeTicks, DateTimeKind.Utc),
        Expiration = dso.ExpirationTicks.HasValue
            ? new DateTime(dso.ExpirationTicks.Value, DateTimeKind.Utc)
            : null,
        ConsumedTime = dso.ConsumedTimeTicks.HasValue
            ? new DateTime(dso.ConsumedTimeTicks.Value, DateTimeKind.Utc)
            : null,
        Data = dso.Data
    };
}
