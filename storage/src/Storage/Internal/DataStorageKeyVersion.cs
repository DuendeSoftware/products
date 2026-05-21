// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.Internal;

public sealed record DataStorageKeyVersion(DataStorageKeyType KeyType, uint SchemaVersion)
{
    public override string ToString() => $"{KeyType.Name}({KeyType.Id}) v{SchemaVersion}";

}
