// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;

namespace Duende.IdentityServer.Hosting.DynamicProviders;

internal class InMemoryIdentityProviderStore : IIdentityProviderStore
{
    private readonly IEnumerable<IEnumerable<IdentityProvider>> _providerCollections;

    // Each call to AddInMemoryIdentityProviders registers the caller's own IEnumerable<IdentityProvider>
    // as a separate singleton. Resolving IEnumerable<IEnumerable<IdentityProvider>> yields every
    // registered collection, so multiple registration calls accumulate instead of shadowing each other.
    // Because each collection is stored by reference (not copied), callers can still mutate their own
    // collection at runtime and have the changes observed here.
    public InMemoryIdentityProviderStore(IEnumerable<IEnumerable<IdentityProvider>> providerCollections) =>
        _providerCollections = providerCollections;

    // Flatten every registered collection and de-duplicate by scheme, keeping the first occurrence.
    // Enumeration order is registration order, so the first registered provider for a given scheme wins.
    // DistinctBy is lazy: it streams providers and only tracks seen schemes, so GetBySchemeAsync can
    // short-circuit on the first match instead of buffering every registered collection up front.
    private IEnumerable<IdentityProvider> Providers =>
        _providerCollections
            .SelectMany(x => x)
            .DistinctBy(x => x.Scheme);

    public Task<IReadOnlyCollection<IdentityProviderName>> GetAllSchemeNamesAsync(Ct ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("InMemoryIdentityProviderStore.GetAllSchemeNames");

        var items = Providers.Select(x => new IdentityProviderName
        {
            Enabled = x.Enabled,
            DisplayName = x.DisplayName,
            Scheme = x.Scheme
        }).ToArray();

        return Task.FromResult<IReadOnlyCollection<IdentityProviderName>>(items);
    }

    public Task<IdentityProvider> GetBySchemeAsync(string scheme, Ct ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("InMemoryIdentityProviderStore.GetByScheme");

        var item = Providers.FirstOrDefault(x => x.Scheme == scheme);
        return Task.FromResult<IdentityProvider>(item);
    }
}
