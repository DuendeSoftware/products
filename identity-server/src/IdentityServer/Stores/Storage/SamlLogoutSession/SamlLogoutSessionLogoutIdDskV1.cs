// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.Storage.Internal;

namespace Duende.IdentityServer.Stores.Storage.SamlLogoutSession;

internal sealed record SamlLogoutSessionLogoutIdDskV1 : IDataStorageKey
{
    private SamlLogoutSessionLogoutIdDskV1(string value) => Value = value;

    public static DataStorageKeyVersion DskVersion { get; } =
        new(SamlLogoutSessionRepository.Keys.LogoutId, 1);

    public string Value { get; }

    public static SamlLogoutSessionLogoutIdDskV1 Create(string logoutId) => new(logoutId);
}
