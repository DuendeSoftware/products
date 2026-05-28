// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.Stores;

/// <summary>
/// Persists and retrieves resource configuration used during authorization decisions.
/// Resources in IdentityServer are of three kinds: identity resources (claims about the
/// user), API scopes (permissions that clients can request), and API resources (logical
/// groupings of API scopes). IdentityServer uses this store to dynamically load resource
/// configuration when validating requested scopes and building tokens.
/// </summary>
public interface IResourceStore
{
    /// <summary>
    /// Gets identity resources whose names match the specified scope names. Identity
    /// resources represent claims about the user (e.g., <c>openid</c>, <c>profile</c>).
    /// </summary>
    /// <param name="scopeNames">The scope names to look up.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>
    /// A read-only collection of <see cref="IdentityResource"/> objects whose names
    /// are contained in <paramref name="scopeNames"/>. Returns an empty collection
    /// when no matches are found.
    /// </returns>
    Task<IReadOnlyCollection<IdentityResource>> FindIdentityResourcesByScopeNameAsync(IEnumerable<string> scopeNames, Ct ct);

    /// <summary>
    /// Gets API scopes whose names match the specified scope names. API scopes represent
    /// permissions that clients can request access to (e.g., <c>api1.read</c>).
    /// </summary>
    /// <param name="scopeNames">The scope names to look up.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>
    /// A read-only collection of <see cref="ApiScope"/> objects whose names are
    /// contained in <paramref name="scopeNames"/>. Returns an empty collection when
    /// no matches are found.
    /// </returns>
    Task<IReadOnlyCollection<ApiScope>> FindApiScopesByNameAsync(IEnumerable<string> scopeNames, Ct ct);

    /// <summary>
    /// Gets API resources that contain at least one of the specified scope names. API
    /// resources are logical groupings of API scopes and carry additional metadata such
    /// as audience values and secrets used for introspection.
    /// </summary>
    /// <param name="scopeNames">The scope names to look up.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>
    /// A read-only collection of <see cref="ApiResource"/> objects that include at
    /// least one scope from <paramref name="scopeNames"/>. Returns an empty collection
    /// when no matches are found.
    /// </returns>
    Task<IReadOnlyCollection<ApiResource>> FindApiResourcesByScopeNameAsync(IEnumerable<string> scopeNames, Ct ct);

    /// <summary>
    /// Gets API resources whose names exactly match the specified API resource names.
    /// </summary>
    /// <param name="apiResourceNames">The API resource names to look up.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>
    /// A read-only collection of <see cref="ApiResource"/> objects whose names are
    /// contained in <paramref name="apiResourceNames"/>. Returns an empty collection
    /// when no matches are found.
    /// </returns>
    Task<IReadOnlyCollection<ApiResource>> FindApiResourcesByNameAsync(IEnumerable<string> apiResourceNames, Ct ct);

    /// <summary>
    /// Gets all resources registered in the store. This is used for discovery and
    /// enumeration scenarios where the complete resource inventory is required.
    /// </summary>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>
    /// A <see cref="Resources"/> object containing all identity resources, API scopes,
    /// and API resources registered in the store.
    /// </returns>
    Task<Resources> GetAllResourcesAsync(Ct ct);
}
