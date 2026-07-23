// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.Storage.Internal;

namespace Duende.IdentityServer.Stores.Storage.SigningKeys;

internal sealed record KeyIdDskV1 : IDataStorageKey
{
    private KeyIdDskV1(string value) => Value = value;

    public static DataStorageKeyVersion DskVersion { get; } =
        new(KeyRepository.Keys.KeyId, 1);

    public string Value { get; }

    public static KeyIdDskV1 Create(string id) => new(id);
}
