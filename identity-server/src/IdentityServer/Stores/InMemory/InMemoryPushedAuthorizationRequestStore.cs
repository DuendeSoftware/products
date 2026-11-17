// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using System.Collections.Concurrent;
using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.Stores;

/// <summary>
/// In-memory implementation of the pushed authorization request store
/// </summary>
public class InMemoryPushedAuthorizationRequestStore : IPushedAuthorizationRequestStore
{
    private readonly PartitionedState<PushedAuthorizationRequestState> _state;

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemoryPushedAuthorizationRequestStore"/> class.
    /// It holds a single partition and isn't split by space; to partition by space use
    /// <see cref="InMemoryPushedAuthorizationRequestStore(InMemoryStoreState)"/>.
    /// </summary>
    public InMemoryPushedAuthorizationRequestStore() : this(new InMemoryStoreState())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemoryPushedAuthorizationRequestStore"/> class
    /// that partitions its data using the given state source.
    /// </summary>
    /// <param name="state">The state source, which decides how data is partitioned.</param>
    public InMemoryPushedAuthorizationRequestStore(InMemoryStoreState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        _state = new(state.CurrentSpace);
    }

    /// <inheritdoc/>
    public Task StoreAsync(PushedAuthorizationRequest pushedAuthorizationRequest, Ct ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("InMemoryPushedAuthorizationRequestStore.Store");

        _state.Current.Items[pushedAuthorizationRequest.ReferenceValueHash] = pushedAuthorizationRequest;

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<PushedAuthorizationRequest?> GetByHashAsync(string referenceValueHash, Ct ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("InMemoryPushedAuthorizationRequestStore.Get");
        if (!_state.TryGetCurrent(out var state))
        {
            return Task.FromResult<PushedAuthorizationRequest?>(null);
        }

        state.Items.TryGetValue(referenceValueHash, out var request);

        return Task.FromResult(request);
    }

    /// <inheritdoc/>
    public Task ConsumeByHashAsync(string referenceValueHash, Ct ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("InMemoryPushedAuthorizationRequestStore.Remove");
        if (_state.TryGetCurrent(out var state))
        {
            state.Items.TryRemove(referenceValueHash, out _);
        }
        return Task.CompletedTask;
    }
}

internal sealed class PushedAuthorizationRequestState
{
    public ConcurrentDictionary<string, PushedAuthorizationRequest> Items { get; } = new();
}
