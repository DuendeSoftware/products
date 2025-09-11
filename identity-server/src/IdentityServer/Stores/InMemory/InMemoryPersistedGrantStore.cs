// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Collections.Concurrent;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.Stores;

/// <summary>
/// In-memory persisted grant store
/// </summary>
public class InMemoryPersistedGrantStore : IPersistedGrantStore
{
    private readonly ConcurrentDictionary<string, PersistedGrant> _repository = new ConcurrentDictionary<string, PersistedGrant>();

    /// <inheritdoc/>
    public Task StoreAsync(PersistedGrant grant)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("InMemoryPersistedGrantStoreResponseGenerator.Store");

        _repository[grant.Key] = grant;

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<PersistedGrant> GetAsync(string key)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("InMemoryPersistedGrantStoreResponseGenerator.Get");

        if (key != null && _repository.TryGetValue(key, out var token))
        {
            return Task.FromResult(token);
        }

        return Task.FromResult<PersistedGrant>(null);
    }

    /// <inheritdoc/>
    public Task<IEnumerable<PersistedGrant>> GetAllAsync(PersistedGrantFilter filter)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("InMemoryPersistedGrantStoreResponseGenerator.GetAll");

        filter.Validate();

        var items = Filter(filter);

        return Task.FromResult(items);
    }

    /// <inheritdoc/>
    public Task RemoveAsync(string key)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("InMemoryPersistedGrantStoreResponseGenerator.Remove");

        _ = _repository.TryRemove(key, out _);

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task RemoveAllAsync(PersistedGrantFilter filter)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("InMemoryPersistedGrantStoreResponseGenerator.RemoveAll");

        filter.Validate();

        var items = Filter(filter);

        foreach (var item in items)
        {
            _ = _repository.TryRemove(item.Key, out _);
        }

        return Task.CompletedTask;
    }

    private IEnumerable<PersistedGrant> Filter(PersistedGrantFilter filter)
    {
        var query =
            from item in _repository
            select item.Value;

        if (filter.ClientIds != null)
        {
            var ids = filter.ClientIds.ToList();
            if (!string.IsNullOrWhiteSpace(filter.ClientId))
            {
                ids.Add(filter.ClientId);
            }
            query = query.Where(x => ids.Contains(x.ClientId));
        }
        else if (!string.IsNullOrWhiteSpace(filter.ClientId))
        {
            query = query.Where(x => x.ClientId == filter.ClientId);
        }

        if (!string.IsNullOrWhiteSpace(filter.SessionId))
        {
            query = query.Where(x => x.SessionId == filter.SessionId);
        }
        if (!string.IsNullOrWhiteSpace(filter.SubjectId))
        {
            query = query.Where(x => x.SubjectId == filter.SubjectId);
        }

        if (filter.Types != null)
        {
            var types = filter.Types.ToList();
            if (!string.IsNullOrWhiteSpace(filter.Type))
            {
                types.Add(filter.Type);
            }
            query = query.Where(x => types.Contains(x.Type));
        }
        else if (!string.IsNullOrWhiteSpace(filter.Type))
        {
            query = query.Where(x => x.Type == filter.Type);
        }

        var items = query.ToArray().AsEnumerable();
        return items;
    }
}
