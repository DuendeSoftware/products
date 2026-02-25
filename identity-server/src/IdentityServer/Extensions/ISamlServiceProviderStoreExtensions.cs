// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.Stores;

/// <summary>
/// Extension methods for ISamlServiceProviderStore
/// </summary>
public static class ISamlServiceProviderStoreExtensions
{
    /// <summary>
    /// Finds an enabled SAML service provider by entity ID.
    /// </summary>
    public static async Task<SamlServiceProvider?> FindEnabledSamlServiceProviderByEntityIdAsync(
        this ISamlServiceProviderStore store, string entityId, Ct ct)
    {
        var sp = await store.FindByEntityIdAsync(entityId, ct);
        if (sp != null && sp.Enabled)
        {
            return sp;
        }
        return null;
    }
}
