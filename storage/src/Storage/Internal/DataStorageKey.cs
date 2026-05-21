// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text.Json;

namespace Duende.Storage.Internal;

public sealed class DataStorageKey
{
    private DataStorageKey(DataStorageKeyVersion version, Guid value, string? keyJsonValue)
    {
        DskVersion = version;
        Value = value;
        KeyJsonValue = keyJsonValue;
    }

    public DataStorageKeyVersion DskVersion { get; private set; }

    public Guid Value { get; private set; }

    public string? KeyJsonValue { get; private set; }

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
