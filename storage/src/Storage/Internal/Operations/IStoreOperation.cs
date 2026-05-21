// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.Internal.Operations;

/// <summary>
/// Marker interface for batch operations.
/// </summary>
public interface IStoreOperation
{
    /// <summary>
    /// Gets the entity type for this operation.
    /// </summary>
    EntityType EntityType { get; }
}
