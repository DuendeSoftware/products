// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.Internal;

/// <summary>
/// Base class for store implementations that provides the space identifier.
/// Stores derive from this to receive the space context at construction time
/// rather than reading it from an ambient ISpaceContextAccessor.
/// </summary>
internal abstract class StoreBase
{
    /// <summary>
    /// The space identifier that scopes all operations performed by this store instance.
    /// </summary>
    protected PoolId PoolId { get; private set; } = -1;

    public void SetPoolId(PoolId poolId) => PoolId = poolId;
}


