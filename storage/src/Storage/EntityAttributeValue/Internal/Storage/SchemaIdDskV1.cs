// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal;

namespace Duende.Storage.EntityAttributeValue.Internal.Storage;

/// <summary>
/// Data storage key for looking up an attribute schema by its <see cref="SchemaId"/>.
/// </summary>
internal sealed record SchemaIdDskV1 : IDataStorageKey
{
    private SchemaIdDskV1(string value) => Value = value;

    /// <summary>Gets the data storage key version descriptor.</summary>
    public static DataStorageKeyVersion DskVersion { get; } =
        new(new DataStorageKeyType(1u, "SchemaId"), 1);

    /// <summary>Gets the schema identifier value.</summary>
    public string Value { get; }

    /// <summary>Creates a key from a <see cref="SchemaId"/>.</summary>
    public static SchemaIdDskV1 Create(SchemaId schemaId) => new(schemaId.Value.ToUpperInvariant());
}
