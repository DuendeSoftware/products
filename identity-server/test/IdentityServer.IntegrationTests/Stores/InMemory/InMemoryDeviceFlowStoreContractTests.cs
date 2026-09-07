// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Stores;

namespace Duende.IdentityServer.IntegrationTests.Stores.InMemory;

/// <summary>
/// InMemory contract tests for IDeviceFlowStore.
/// Note: xUnit v3 creates a new instance per test method, so the backing store
/// starts fresh for each test — providing natural test isolation.
/// </summary>
public class InMemoryDeviceFlowStoreContractTests : DeviceFlowStoreContractTests
{
    private readonly InMemoryDeviceFlowStore _store = new();

    protected override StoreHandle<IDeviceFlowStore> CreateStore() => new(_store);
}
