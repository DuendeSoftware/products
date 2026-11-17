// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Duende.Spaces;

namespace Duende.IdentityServer.Stores;

internal sealed class PartitionedState<TState> where TState : class, new()
{
    private readonly GetSpaceIdOrDefault _currentSpace;
    private readonly ConcurrentDictionary<SpaceId, TState> _partitions = new();

    public PartitionedState(GetSpaceIdOrDefault currentSpace) => _currentSpace = currentSpace;

    public TState Current => _partitions.GetOrAdd(_currentSpace(), static _ => new TState());

    // For reads and removes: looking in a space without data does not create a partition
    // (the session cleanup visits every partition).
    public bool TryGetCurrent([NotNullWhen(true)] out TState? state) => _partitions.TryGetValue(_currentSpace(), out state);

    public IEnumerable<KeyValuePair<SpaceId, TState>> Partitions => _partitions.ToArray();
}
