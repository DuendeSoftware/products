// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.Stores;

/// <summary>
/// In-memory resource store
/// </summary>
public class InMemoryResourcesStore : IResourceStore
{
    private readonly IEnumerable<IdentityResource> _identityResources;
    private readonly IEnumerable<ApiResource> _apiResources;
    private readonly IEnumerable<ApiScope> _apiScopes;

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemoryResourcesStore" /> class.
    /// </summary>
    public InMemoryResourcesStore(
        IEnumerable<IdentityResource> identityResources = null,
        IEnumerable<ApiResource> apiResources = null,
        IEnumerable<ApiScope> apiScopes = null)
    {
        if (identityResources?.HasDuplicates(m => m.Name) == true)
        {
            throw new ArgumentException("Identity resources must not contain duplicate names");
        }

        if (apiResources?.HasDuplicates(m => m.Name) == true)
        {
            throw new ArgumentException("Api resources must not contain duplicate names");
        }

        if (apiScopes?.HasDuplicates(m => m.Name) == true)
        {
            throw new ArgumentException("Scopes must not contain duplicate names");
        }

        _identityResources = identityResources ?? Enumerable.Empty<IdentityResource>();
        _apiResources = apiResources ?? Enumerable.Empty<ApiResource>();
        _apiScopes = apiScopes ?? Enumerable.Empty<ApiScope>();
    }

    /// <inheritdoc/>
    public Task<Resources> GetAllResourcesAsync(CT ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("InMemoryResourceStore.GetAllResources");

        var result = new Resources(_identityResources, _apiResources, _apiScopes);
        return Task.FromResult(result);
    }

    /// <inheritdoc/>
    public Task<IEnumerable<ApiResource>> FindApiResourcesByNameAsync(IEnumerable<string> apiResourceNames, CT ct)
    {
        ArgumentNullException.ThrowIfNull(apiResourceNames);
        using var activity = Tracing.StoreActivitySource.StartActivity("InMemoryResourceStore.FindApiResourcesByName");
        activity?.SetTag(Tracing.Properties.ApiResourceNames, apiResourceNames.ToSpaceSeparatedString());

        var query = from a in _apiResources
                    where apiResourceNames.Contains(a.Name)
                    select a;
        return Task.FromResult(query);
    }

    /// <inheritdoc/>
    public Task<IEnumerable<IdentityResource>> FindIdentityResourcesByScopeNameAsync(IEnumerable<string> scopeNames, CT ct)
    {
        ArgumentNullException.ThrowIfNull(scopeNames);
        using var activity = Tracing.StoreActivitySource.StartActivity("InMemoryResourceStore.FindIdentityResourcesByScopeName");
        activity?.SetTag(Tracing.Properties.ScopeNames, scopeNames.ToSpaceSeparatedString());

        var identity = from i in _identityResources
                       where scopeNames.Contains(i.Name)
                       select i;

        return Task.FromResult(identity);
    }

    /// <inheritdoc/>
    public Task<IEnumerable<ApiResource>> FindApiResourcesByScopeNameAsync(IEnumerable<string> scopeNames, CT ct)
    {
        ArgumentNullException.ThrowIfNull(scopeNames);
        using var activity = Tracing.StoreActivitySource.StartActivity("InMemoryResourceStore.FindApiResourcesByScopeName");
        activity?.SetTag(Tracing.Properties.ScopeNames, scopeNames.ToSpaceSeparatedString());

        var query = from a in _apiResources
                    where a.Scopes.Any(x => scopeNames.Contains(x))
                    select a;

        return Task.FromResult(query);
    }

    /// <inheritdoc/>
    public Task<IEnumerable<ApiScope>> FindApiScopesByNameAsync(IEnumerable<string> scopeNames, CT ct)
    {
        ArgumentNullException.ThrowIfNull(scopeNames);
        using var activity = Tracing.StoreActivitySource.StartActivity("InMemoryResourceStore.FindApiScopesByName");
        activity?.SetTag(Tracing.Properties.ScopeNames, scopeNames.ToSpaceSeparatedString());

        var query =
            from x in _apiScopes
            where scopeNames.Contains(x.Name)
            select x;

        return Task.FromResult(query);
    }
}
