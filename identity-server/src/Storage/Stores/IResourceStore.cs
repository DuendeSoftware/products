// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.Stores;

/// <summary>
/// Resource retrieval
/// </summary>
public interface IResourceStore
{
    /// <summary>
    /// Gets identity resources by scope name.
    /// </summary>
    /// <param name="scopeNames">The scope names.</param>
    /// <param name="ct">The <see cref="CT"/> used to propagate notifications that the operation should be cancelled.</param>
    Task<IEnumerable<IdentityResource>> FindIdentityResourcesByScopeNameAsync(IEnumerable<string> scopeNames, CT ct);

    /// <summary>
    /// Gets API scopes by scope name.
    /// </summary>
    /// <param name="scopeNames">The scope names.</param>
    /// <param name="ct">The <see cref="CT"/> used to propagate notifications that the operation should be cancelled.</param>
    Task<IEnumerable<ApiScope>> FindApiScopesByNameAsync(IEnumerable<string> scopeNames, CT ct);

    /// <summary>
    /// Gets API resources by scope name.
    /// </summary>
    /// <param name="scopeNames">The scope names.</param>
    /// <param name="ct">The <see cref="CT"/> used to propagate notifications that the operation should be cancelled.</param>
    Task<IEnumerable<ApiResource>> FindApiResourcesByScopeNameAsync(IEnumerable<string> scopeNames, CT ct);

    /// <summary>
    /// Gets API resources by API resource name.
    /// </summary>
    /// <param name="apiResourceNames">The API resource names.</param>
    /// <param name="ct">The <see cref="CT"/> used to propagate notifications that the operation should be cancelled.</param>
    Task<IEnumerable<ApiResource>> FindApiResourcesByNameAsync(IEnumerable<string> apiResourceNames, CT ct);

    /// <summary>
    /// Gets all resources.
    /// </summary>
    /// <param name="ct">The <see cref="CT"/> used to propagate notifications that the operation should be cancelled.</param>
    Task<Resources> GetAllResourcesAsync(CT ct);
}
