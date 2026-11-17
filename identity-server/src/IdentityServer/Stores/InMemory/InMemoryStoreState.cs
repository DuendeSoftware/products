// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Spaces;

namespace Duende.IdentityServer.Stores;

/// <summary>
/// The state source for the in-memory operational stores. It is resolved from dependency injection
/// and passed to the in-memory store constructors that accept it. When Spaces is enabled, stores
/// built with it keep each space's data apart (partitioned by the current space, where no space
/// and the default space share data). Without Spaces there is a single partition.
/// </summary>
public sealed class InMemoryStoreState
{
    internal InMemoryStoreState(GetSpaceIdOrDefault currentSpace) => CurrentSpace = currentSpace;

    internal InMemoryStoreState() : this(static () => SpaceId.Default)
    {
    }

    internal GetSpaceIdOrDefault CurrentSpace { get; }
}
