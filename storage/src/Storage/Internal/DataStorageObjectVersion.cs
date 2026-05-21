// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.Internal;

/// <summary>
/// Represents a versioned DSO type.
/// </summary>
public sealed record DataStorageObjectVersion(EntityType EntityType, uint SchemaVersion)
{
    public override string ToString() => $"{EntityType.Name}({EntityType.Id}) v{SchemaVersion}";
}
