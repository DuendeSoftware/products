// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.Storage.Internal;

namespace Duende.IdentityServer.Stores.Storage.IdentityProviders;

internal sealed record IdentityProviderSchemeDskV1 : IDataStorageKey
{
    private IdentityProviderSchemeDskV1(string value) => Value = value;

    public static DataStorageKeyVersion DskVersion { get; } =
        new(IdentityProviderRepository.Keys.Scheme, 1);

    public string Value { get; }

    public static IdentityProviderSchemeDskV1 Create(string scheme) => new(scheme);
}
