// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.Storage.Internal;

namespace Duende.IdentityServer.Stores.Storage.SamlLogoutSession;

internal sealed record SamlLogoutSessionRequestIdDskV1 : IDataStorageKey
{
    private SamlLogoutSessionRequestIdDskV1(string value) => Value = value;

    public static DataStorageKeyVersion DskVersion { get; } =
        new(SamlLogoutSessionRepository.Keys.RequestId, 1);

    public string Value { get; }

    public static SamlLogoutSessionRequestIdDskV1 Create(string requestId) => new(requestId);
}
