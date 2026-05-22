// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.Internal;

/// <summary>
/// Represents a versioned data storage key type, combining the key type with its schema version.
/// </summary>
/// <remarks>
/// This type is for usage by Duende Software products, is not supported for end user consumption, and not subject to semantic versioning rules.
/// </remarks>
/// <param name="KeyType">The type of the data storage key.</param>
/// <param name="SchemaVersion">The schema version of the key.</param>
public sealed record DataStorageKeyVersion(DataStorageKeyType KeyType, uint SchemaVersion)
{
    /// <inheritdoc />
    public override string ToString() => $"{KeyType.Name}({KeyType.Id}) v{SchemaVersion}";

}
