// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.Stores.Empty;

internal class EmptyResourceStore : IResourceStore
{
    public Task<IReadOnlyCollection<ApiResource>> FindApiResourcesByNameAsync(IEnumerable<string> apiResourceNames, Ct _) => Task.FromResult<IReadOnlyCollection<ApiResource>>(Array.Empty<ApiResource>());

    public Task<IReadOnlyCollection<ApiResource>> FindApiResourcesByScopeNameAsync(IEnumerable<string> scopeNames, Ct _) => Task.FromResult<IReadOnlyCollection<ApiResource>>(Array.Empty<ApiResource>());

    public Task<IReadOnlyCollection<ApiScope>> FindApiScopesByNameAsync(IEnumerable<string> scopeNames, Ct _) => Task.FromResult<IReadOnlyCollection<ApiScope>>(Array.Empty<ApiScope>());

    public Task<IReadOnlyCollection<IdentityResource>> FindIdentityResourcesByScopeNameAsync(IEnumerable<string> scopeNames, Ct _) => Task.FromResult<IReadOnlyCollection<IdentityResource>>(Array.Empty<IdentityResource>());

    public Task<Resources> GetAllResourcesAsync(Ct _) => Task.FromResult(new Resources() { OfflineAccess = true });
}
