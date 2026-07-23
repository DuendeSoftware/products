// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.Storage.Internal;

namespace Duende.IdentityServer.Stores.Storage.ServerSideSessions;

internal sealed record SessionKeyDskV1 : IDataStorageKey
{
    private SessionKeyDskV1(string value) => Value = value;

    public static DataStorageKeyVersion DskVersion { get; } =
        new(ServerSideSessionRepository.Keys.SessionKey, 1);

    public string Value { get; }

    public static SessionKeyDskV1 Create(string key) => new(key);
}
