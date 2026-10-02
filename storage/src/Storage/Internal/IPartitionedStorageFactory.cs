// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.Internal;

/// <summary>
/// Factory for obtaining a store scoped to the current space context.
/// </summary>
public interface IPartitionedStorageFactory
{
    /// <summary>
    /// Gets a store scoped to the current space. The space is determined by the
    /// ambient context (e.g. <c>ISpaceContextAccessor</c>) rather than an explicit parameter.
    /// </summary>
    /// <param name="dataCategory">The storage category identifying which storage instance to use.</param>
    /// <param name="ct">A cancellation token.</param>
    Task<IPartitionedStorage> GetPartitionedStorageAsync(DataCategoryName dataCategory, Ct ct);

}
