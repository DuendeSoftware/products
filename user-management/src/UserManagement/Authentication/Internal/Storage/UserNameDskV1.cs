// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal;

namespace Duende.UserManagement.Authentication.Internal.Storage;

internal sealed record UserNameDskV1 : IDataStorageKey
{
    private UserNameDskV1(string value) => Value = value;

    public static DataStorageKeyVersion DskVersion { get; } =
        new(UserAuthenticatorsRepository.Keys.UserName, 1);

    public string Value { get; }

    public static UserNameDskV1 Create(UserName userName) => new(userName.Value);
}
