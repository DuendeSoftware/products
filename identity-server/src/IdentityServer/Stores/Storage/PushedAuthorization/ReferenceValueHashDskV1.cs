// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.Storage.Internal;

namespace Duende.IdentityServer.Stores.Storage.PushedAuthorization;

internal sealed record ReferenceValueHashDskV1 : IDataStorageKey
{
    private ReferenceValueHashDskV1(string value) => Value = value;

    public static DataStorageKeyVersion DskVersion { get; } =
        new(PushedAuthorizationRepository.Keys.ReferenceValueHash, 1);

    public string Value { get; }

    public static ReferenceValueHashDskV1 Create(string referenceValueHash) => new(referenceValueHash);
}
