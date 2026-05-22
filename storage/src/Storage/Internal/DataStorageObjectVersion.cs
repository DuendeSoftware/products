// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.Internal;

/// <summary>
/// Represents a versioned DSO type.
/// </summary>
/// <remarks>
/// This type is for usage by Duende Software products, is not supported for end user consumption, and not subject to semantic versioning rules.
/// </remarks>
public sealed record DataStorageObjectVersion(EntityType EntityType, uint SchemaVersion)
{
    public override string ToString() => $"{EntityType.Name}({EntityType.Id}) v{SchemaVersion}";
}
