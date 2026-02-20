// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.Stores;

/// <summary>
/// Interface to model storage of identity providers.
/// </summary>
public interface IIdentityProviderStore
{
    /// <summary>
    /// Gets all identity providers name.
    /// </summary>
    /// <param name="ct"></param>
    Task<IEnumerable<IdentityProviderName>> GetAllSchemeNamesAsync(CT ct = default);

    /// <summary>
    /// Gets the identity provider by scheme name.
    /// </summary>
    /// <param name="scheme"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    Task<IdentityProvider?> GetBySchemeAsync(string scheme, CT ct = default);
}
