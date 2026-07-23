// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.Storage.Internal;

namespace Duende.IdentityServer.Stores.Storage.ApiScopes;

internal sealed record ApiScopeNameDskV1 : IDataStorageKey
{
    private ApiScopeNameDskV1(string value) => Value = value;

    public static DataStorageKeyVersion DskVersion { get; } =
        new(ApiScopeRepository.Keys.Name, 1);

    public string Value { get; }

    public static ApiScopeNameDskV1 Create(string name) => new(name);
}
