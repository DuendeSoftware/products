// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.Storage.Internal;

namespace Duende.IdentityServer.Stores.Storage.IdentityResources;

internal sealed record IdentityResourceNameDskV1 : IDataStorageKey
{
    private IdentityResourceNameDskV1(string value) => Value = value;

    public static DataStorageKeyVersion DskVersion { get; } =
        new(IdentityResourceRepository.Keys.Name, 1);

    public string Value { get; }

    public static IdentityResourceNameDskV1 Create(string name) => new(name);
}
