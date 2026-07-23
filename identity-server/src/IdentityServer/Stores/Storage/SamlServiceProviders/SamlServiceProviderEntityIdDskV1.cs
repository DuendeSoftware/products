// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.Storage.Internal;

namespace Duende.IdentityServer.Stores.Storage.SamlServiceProviders;

internal sealed record SamlServiceProviderEntityIdDskV1 : IDataStorageKey
{
    private SamlServiceProviderEntityIdDskV1(string value) => Value = value;

    public static DataStorageKeyVersion DskVersion { get; } =
        new(SamlServiceProviderRepository.Keys.EntityId, 1);

    public string Value { get; }

    public static SamlServiceProviderEntityIdDskV1 Create(string entityId) => new(entityId);
}
