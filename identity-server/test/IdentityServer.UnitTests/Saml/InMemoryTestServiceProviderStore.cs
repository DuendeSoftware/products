// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;

namespace UnitTests.Saml;

/// <summary>
/// Simple in-memory implementation of ISamlServiceProviderStore for unit testing.
/// </summary>
internal class InMemoryTestServiceProviderStore : ISamlServiceProviderStore
{
    private readonly List<SamlServiceProvider> _providers = [];

    public InMemoryTestServiceProviderStore() { }
    public InMemoryTestServiceProviderStore(params SamlServiceProvider[] providers) => _providers.AddRange(providers);

    public void Add(SamlServiceProvider provider) => _providers.Add(provider);

    public Task<SamlServiceProvider?> FindByEntityIdAsync(string entityId)
        => Task.FromResult(_providers.FirstOrDefault(p => p.EntityId == entityId));
}
