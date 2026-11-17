// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.Storage.Internal;

namespace Duende.Spaces.Internal.Storage;

internal sealed record SpacePoolIdHighWaterMarkDskV1 : IGuidDataStorageKey
{
    public static DataStorageKeyVersion DskVersion { get; } =
        new(new DataStorageKeyType(200_003u, "SpacePoolIdHighWaterMark"), 1);

    public Guid Value => Guid.Empty;
}
