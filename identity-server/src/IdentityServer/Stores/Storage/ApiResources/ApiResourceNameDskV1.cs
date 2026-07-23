// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.Storage.Internal;

namespace Duende.IdentityServer.Stores.Storage.ApiResources;

internal sealed record ApiResourceNameDskV1 : IDataStorageKey
{
    private ApiResourceNameDskV1(string value) => Value = value;

    public static DataStorageKeyVersion DskVersion { get; } =
        new(ApiResourceRepository.Keys.Name, 1);

    public string Value { get; }

    public static ApiResourceNameDskV1 Create(string name) => new(name);
}
