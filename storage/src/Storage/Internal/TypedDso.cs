// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.Internal;

public sealed record TypedDso
{
    public static TypedDso For<TDso>(TDso value) where TDso : IDataStorageObject => new TypedDso
    {
        Value = value,
        EntityType = TDso.DsoVersion.EntityType,
        Version = TDso.DsoVersion
    };

    public required IDataStorageObject Value { get; init; }
    public required EntityType EntityType { get; init; }
    public required DataStorageObjectVersion Version { get; init; }
}
