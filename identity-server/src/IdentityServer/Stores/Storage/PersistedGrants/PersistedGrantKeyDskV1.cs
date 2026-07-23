// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.Storage.Internal;

namespace Duende.IdentityServer.Stores.Storage.PersistedGrants;

internal sealed record PersistedGrantKeyDskV1 : IDataStorageKey
{
    private PersistedGrantKeyDskV1(string value) => Value = value;

    public static DataStorageKeyVersion DskVersion { get; } =
        new(PersistedGrantRepository.Keys.GrantKey, 1);

    public string Value { get; }

    public static PersistedGrantKeyDskV1 Create(string key) => new(key);
}
