// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.Storage.Internal;

namespace Duende.IdentityServer.Infrastructure.DistributedLock;

internal sealed record LockNameDskV1 : IDataStorageKey
{
    private LockNameDskV1(string value) => Value = value;

    internal enum Keys
    {
        Name = 1
    }

    public static DataStorageKeyVersion DskVersion { get; } = new(Keys.Name, 1);

    public string Value { get; }

    public static LockNameDskV1 Create(LockName name) => new(name.Value);
}
