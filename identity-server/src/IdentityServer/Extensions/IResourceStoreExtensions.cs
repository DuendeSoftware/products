// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.Stores;

/// <summary>
/// Extensions for IResourceStore
/// </summary>
public static class IResourceStoreExtensions
{
    /// <summary>
    /// Finds the resources by scope.
    /// </summary>
    /// <param name="store">The store.</param>
    /// <param name="scopeNames">The scope names.</param>
    /// <returns></returns>
    public static async Task<Resources> FindResourcesByScopeAsync(this IResourceStore store, IEnumerable<string> scopeNames)
    {
        var identity = await store.FindIdentityResourcesByScopeNameAsync(scopeNames);
        var apiResources = await store.FindApiResourcesByScopeNameAsync(scopeNames);
        var scopes = await store.FindApiScopesByNameAsync(scopeNames);

        ValidateNameUniqueness(identity, apiResources, scopes);

        var resources = new Resources(identity, apiResources, scopes)
        {
            OfflineAccess = scopeNames.Contains(IdentityServerConstants.StandardScopes.OfflineAccess)
        };

        return resources;
    }

    private static void ValidateNameUniqueness(IEnumerable<IdentityResource> identity, IEnumerable<ApiResource> apiResources, IEnumerable<ApiScope> apiScopes)
    {
        // attempt to detect invalid configuration. this is about the only place
        // we can do this, since it's hard to get the values in the store.
        var identityScopeNames = identity.Select(x => x.Name).ToArray();
        var dups = GetDuplicates(identityScopeNames);
        if (dups.Length > 0)
        {
            var names = dups.Aggregate((x, y) => x + ", " + y);
            throw new Exception(
                $"Duplicate identity scopes found. This is an invalid configuration. Use different names for identity scopes. Scopes found: {names}");
        }

        var apiNames = apiResources.Select(x => x.Name);
        dups = GetDuplicates(apiNames);
        if (dups.Length > 0)
        {
            var names = dups.Aggregate((x, y) => x + ", " + y);
            throw new Exception(
                $"Duplicate api resources found. This is an invalid configuration. Use different names for API resources. Names found: {names}");
        }

        var scopesNames = apiScopes.Select(x => x.Name);
        dups = GetDuplicates(scopesNames);
        if (dups.Length > 0)
        {
            var names = dups.Aggregate((x, y) => x + ", " + y);
            throw new Exception(
                $"Duplicate scopes found. This is an invalid configuration. Use different names for scopes. Names found: {names}");
        }

        var overlap = identityScopeNames.Intersect(scopesNames).ToArray();
        if (overlap.Length > 0)
        {
            var names = overlap.Aggregate((x, y) => x + ", " + y);
            throw new Exception(
                $"Found identity scopes and API scopes that use the same names. This is an invalid configuration. Use different names for identity scopes and API scopes. Scopes found: {names}");
        }
    }

    private static string[] GetDuplicates(IEnumerable<string> names)
    {
        var duplicates = names
            .GroupBy(x => x)
            .Where(g => g.Count() > 1)
            .Select(y => y.Key)
            .ToArray();
        return duplicates.ToArray();
    }

    /// <summary>
    /// Finds the enabled resources by scope.
    /// </summary>
    /// <param name="store">The store.</param>
    /// <param name="scopeNames">The scope names.</param>
    /// <returns></returns>
    public static async Task<Resources> FindEnabledResourcesByScopeAsync(this IResourceStore store, IEnumerable<string> scopeNames) => (await store.FindResourcesByScopeAsync(scopeNames)).FilterEnabled();

    /// <summary>
    /// Gets all enabled resources.
    /// </summary>
    /// <param name="store">The store.</param>
    /// <returns></returns>
    public static async Task<Resources> GetAllEnabledResourcesAsync(this IResourceStore store)
    {
        var resources = await store.GetAllResourcesAsync();
        ValidateNameUniqueness(resources.IdentityResources, resources.ApiResources, resources.ApiScopes);

        return resources.FilterEnabled();
    }

    /// <summary>
    /// Finds the enabled identity resources by scope.
    /// </summary>
    /// <param name="store">The store.</param>
    /// <param name="scopeNames">The scope names.</param>
    /// <returns></returns>
    public static async Task<IEnumerable<IdentityResource>> FindEnabledIdentityResourcesByScopeAsync(this IResourceStore store, IEnumerable<string> scopeNames) => (await store.FindIdentityResourcesByScopeNameAsync(scopeNames)).Where(x => x.Enabled).ToArray();

    /// <summary>
    /// Finds the enabled API resources by name.
    /// </summary>
    public static async Task<IEnumerable<ApiResource>> FindEnabledApiResourcesByNameAsync(this IResourceStore store, IEnumerable<string> resourceNames) => (await store.FindApiResourcesByNameAsync(resourceNames)).Where(x => x.Enabled).ToArray();
}
