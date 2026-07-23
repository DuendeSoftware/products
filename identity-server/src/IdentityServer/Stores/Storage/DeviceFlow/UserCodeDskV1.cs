// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.Storage.Internal;

namespace Duende.IdentityServer.Stores.Storage.DeviceFlow;

internal sealed record UserCodeDskV1 : IDataStorageKey
{
    private UserCodeDskV1(string value) => Value = value;

    public static DataStorageKeyVersion DskVersion { get; } =
        new(DeviceFlowRepository.Keys.UserCode, 1);

    public string Value { get; }

    public static UserCodeDskV1 Create(string userCode) => new(userCode);
}
