// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.Storage.Internal;

namespace Duende.IdentityServer.Stores.Storage;

/// <summary>
/// The exact, case-sensitive name key that makes a <see cref="ScopeNameDso"/> unique across API scopes
/// and identity resources.
/// </summary>
internal sealed record ScopeNameGuardDskV1 : IDataStorageKey
{
    private ScopeNameGuardDskV1(string value) => Value = value;

    public static DataStorageKeyVersion DskVersion { get; } =
        new(ScopeNameRepository.Keys.Name, 1);

    public string Value { get; }

    public static ScopeNameGuardDskV1 Create(string name) => new(name);
}
