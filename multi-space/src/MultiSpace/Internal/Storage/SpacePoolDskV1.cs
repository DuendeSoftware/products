// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal;

namespace Duende.MultiSpace.Internal.Storage;

internal sealed record SpacePoolDskV1 : IDataStorageKey
{
    private SpacePoolDskV1(int poolId) => PoolId = poolId;

    public static DataStorageKeyVersion DskVersion { get; } =
        new(new DataStorageKeyType(200_002u, "SpacePool"), 1);

    public int PoolId { get; }

    public static SpacePoolDskV1 Create(int poolId) => new(poolId);
}
