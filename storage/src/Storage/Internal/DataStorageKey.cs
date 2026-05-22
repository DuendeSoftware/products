// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text.Json;

namespace Duende.Storage.Internal;

/// <summary>
/// Represents a key used for alternate lookups in the data store.
/// </summary>
/// <remarks>
/// This type is for usage by Duende Software products, is not supported for end user consumption, and not subject to semantic versioning rules.
/// </remarks>
public sealed class DataStorageKey
{
    private DataStorageKey(DataStorageKeyVersion version, Guid value, string? keyJsonValue)
    {
        DskVersion = version;
        Value = value;
        KeyJsonValue = keyJsonValue;
    }

    /// <summary>
    /// Gets the version of the data storage key.
    /// </summary>
    public DataStorageKeyVersion DskVersion { get; private set; }

    /// <summary>
    /// Gets the GUID value of the key.
    /// </summary>
    public Guid Value { get; private set; }

    /// <summary>
    /// Gets the JSON representation of the key, or <c>null</c> for GUID-based keys.
    /// </summary>
    public string? KeyJsonValue { get; private set; }

    /// <summary>
    /// Creates a <see cref="DataStorageKey"/> from the specified key instance.
    /// </summary>
    /// <typeparam name="T">The type of the data storage key.</typeparam>
    /// <param name="dsk">The data storage key instance.</param>
    /// <returns>A new <see cref="DataStorageKey"/>.</returns>
    public static DataStorageKey Create<T>(T dsk) where T : IDataStorageKey
    {
        if (dsk is IGuidDataStorageKey guidDsk)
        {
            return new DataStorageKey(T.DskVersion, guidDsk.Value, null);
        }

        var json = JsonSerializer.Serialize(dsk);
        var guid = DeterministicGuidGenerator.Create(json);

        return new DataStorageKey(T.DskVersion, guid, json);
    }
}
