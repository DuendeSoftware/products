// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.Internal;

/// <summary>
/// Represents a pooled store that supports multiple isolated pools sharing a single database.
/// </summary>
/// <remarks>
/// This type is for usage by Duende Software products, is not supported for end user consumption, and not subject to semantic versioning rules.
/// </remarks>
public interface IPooledStore : IDatabaseSchema
{
    /// <summary>
    /// Opens a store scoped to the specified pool.
    /// </summary>
    /// <param name="poolId">The pool identifier.</param>
    /// <returns>An <see cref="IStore"/> scoped to the specified pool.</returns>
    IStore OpenPool(PoolId poolId);
}
