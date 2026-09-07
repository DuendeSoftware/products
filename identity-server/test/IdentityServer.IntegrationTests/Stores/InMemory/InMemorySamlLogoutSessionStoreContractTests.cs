// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Saml;
using Duende.IdentityServer.Stores;
using Microsoft.Extensions.Logging.Abstractions;

namespace Duende.IdentityServer.IntegrationTests.Stores.InMemory;

/// <summary>
/// InMemory contract tests for ISamlLogoutSessionStore.
/// Note: xUnit v3 creates a new instance per test method, so the backing store
/// starts empty for each test — providing natural test isolation.
/// </summary>
public class InMemorySamlLogoutSessionStoreContractTests : SamlLogoutSessionStoreContractTests
{
    private readonly InMemorySamlLogoutSessionStore _store = new(
        TimeProvider.System,
        NullLogger<InMemorySamlLogoutSessionStore>.Instance);

    protected override StoreHandle<ISamlLogoutSessionStore> CreateStore() => new(_store);
}
