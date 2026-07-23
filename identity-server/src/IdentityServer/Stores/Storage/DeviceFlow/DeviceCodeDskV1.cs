// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.Storage.Internal;

namespace Duende.IdentityServer.Stores.Storage.DeviceFlow;

internal sealed record DeviceCodeDskV1 : IDataStorageKey
{
    private DeviceCodeDskV1(string value) => Value = value;

    public static DataStorageKeyVersion DskVersion { get; } =
        new(DeviceFlowRepository.Keys.DeviceCode, 1);

    public string Value { get; }

    public static DeviceCodeDskV1 Create(string deviceCode) => new(deviceCode);
}
