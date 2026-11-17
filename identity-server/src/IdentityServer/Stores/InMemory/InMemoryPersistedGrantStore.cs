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
    private readonly PartitionedState<PersistedGrantState> _state;

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemoryPersistedGrantStore"/> class.
    /// It holds a single partition and isn't split by space; to partition by space use
    /// <see cref="InMemoryPersistedGrantStore(InMemoryStoreState)"/>.
    /// </summary>
    public InMemoryPersistedGrantStore() : this(new InMemoryStoreState())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemoryPersistedGrantStore"/> class
    /// that partitions its data using the given state source.
    /// </summary>
    /// <param name="state">The state source, which decides how data is partitioned.</param>
    public InMemoryPersistedGrantStore(InMemoryStoreState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        _state = new(state.CurrentSpace);
    }

    /// <inheritdoc/>
    public Task StoreAsync(PersistedGrant grant, Ct ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("InMemoryPersistedGrantStoreResponseGenerator.Store");

        _state.Current.Items[grant.Key] = grant;

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<PersistedGrant> GetAsync(string key, Ct ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("InMemoryPersistedGrantStoreResponseGenerator.Get");

        if (key != null && _state.TryGetCurrent(out var state) && state.Items.TryGetValue(key, out var token))
        {
            return Task.FromResult(token);
        }

        return Task.FromResult<PersistedGrant>(null);
    }

    /// <inheritdoc/>
    public Task<IReadOnlyCollection<PersistedGrant>> GetAllAsync(PersistedGrantFilter filter, Ct ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("InMemoryPersistedGrantStoreResponseGenerator.GetAll");

        filter.Validate();

        if (!_state.TryGetCurrent(out var state))
        {
            return Task.FromResult<IReadOnlyCollection<PersistedGrant>>([]);
        }

        var items = Filter(state, filter);

        return Task.FromResult<IReadOnlyCollection<PersistedGrant>>(items);
    }

    /// <inheritdoc/>
    public Task RemoveAsync(string key, Ct ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("InMemoryPersistedGrantStoreResponseGenerator.Remove");

        if (_state.TryGetCurrent(out var state))
        {
            state.Items.TryRemove(key, out _);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task RemoveAllAsync(PersistedGrantFilter filter, Ct ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("InMemoryPersistedGrantStoreResponseGenerator.RemoveAll");

        filter.Validate();

        if (!_state.TryGetCurrent(out var state))
        {
            return Task.CompletedTask;
        }

        var items = Filter(state, filter);

        foreach (var item in items)
        {
            state.Items.TryRemove(item.Key, out _);
        }

        return Task.CompletedTask;
    }

    private static PersistedGrant[] Filter(PersistedGrantState state, PersistedGrantFilter filter)
    {
        var query =
            from item in state.Items
            select item.Value;

        if (filter.ClientIds.Count > 0)
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

        if (filter.Types.Count > 0)
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

        var items = query.ToArray();
        return items;
    }
}

internal sealed class PersistedGrantState
{
    public ConcurrentDictionary<string, PersistedGrant> Items { get; } = new();
}
