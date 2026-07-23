// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.Storage.Internal;

namespace Duende.IdentityServer.Stores.Storage.Clients;

internal sealed record ClientIdDskV1 : IDataStorageKey
{
    private ClientIdDskV1(string value) => Value = value;

    public static DataStorageKeyVersion DskVersion { get; } =
        new(ClientRepository.Keys.ClientId, 1);

    public string Value { get; }

    public static ClientIdDskV1 Create(string clientId) => new(clientId);
}
