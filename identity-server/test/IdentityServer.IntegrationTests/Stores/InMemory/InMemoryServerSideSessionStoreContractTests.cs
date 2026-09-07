// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Stores;

namespace Duende.IdentityServer.IntegrationTests.Stores.InMemory;

/// <summary>
/// InMemory contract tests for IServerSideSessionStore.
/// Note: xUnit v3 creates a new instance per test method, so the backing store
/// starts empty for each test — providing natural test isolation.
/// </summary>
public class InMemoryServerSideSessionStoreContractTests : ServerSideSessionStoreContractTests
{
    protected override StoreHandle<IServerSideSessionStore> CreateStore() =>
        new(new InMemoryServerSideSessionStore(TimeProvider.System));
}
