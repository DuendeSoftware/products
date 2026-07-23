// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal;

namespace Duende.MultiSpace;

internal sealed record TestDso(string Value) : IDataStorageObject
{
    public static DataStorageObjectVersion DsoVersion { get; } = new(new EntityType(99, nameof(TestDso)), 1);
}
