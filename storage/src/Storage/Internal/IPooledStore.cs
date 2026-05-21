// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.Internal;

public interface IPooledStore : IDatabaseSchema
{
    IStore OpenPool(PoolId poolId);
}
