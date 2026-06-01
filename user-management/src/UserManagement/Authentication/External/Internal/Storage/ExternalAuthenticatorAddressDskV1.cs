// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal;
using Duende.UserManagement.Authentication.Internal.Storage;

namespace Duende.UserManagement.Authentication.External.Internal.Storage;

internal sealed record ExternalAuthenticatorAddressDskV1 : IDataStorageKey
{
    private ExternalAuthenticatorAddressDskV1(string name, string id)
    {
        Name = name;
        Id = id;
    }

    public static DataStorageKeyVersion DskVersion { get; } =
        new(UserAuthenticatorsRepository.Keys.ExternalAuthenticator, 1);

    /// <summary>
    /// Name of the external authenticator (IE: Google, Facebook, etc).
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// The ID that's provided by the external authenticator.
    /// </summary>
    public string Id { get; }

    public static ExternalAuthenticatorAddressDskV1 Create(ExternalAuthenticatorAddress address)
        => new(address.Name.ToString(), address.SubjectId.ToString());
}
