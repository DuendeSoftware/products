// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal;

namespace UnitTests.Common;

internal sealed class SimpleStorageFactory(IStorage storage) : IStorageFactory
{
    public Task<IStorage> GetStorage(CancellationToken _) => Task.FromResult(storage);
}

internal sealed class ThrowingStorageFactory : IStorageFactory
{
    public Task<IStorage> GetStorage(CancellationToken _) => throw new InvalidOperationException("Simulated factory failure");
}
