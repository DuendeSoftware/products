// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.Stores;

/// <summary>
/// Extension for IClientStore
/// </summary>
public static class IClientStoreExtensions
{
    /// <summary>
    /// Finds the enabled client by identifier.
    /// </summary>
    /// <param name="store">The store.</param>
    /// <param name="clientId">The client identifier.</param>
    /// <returns></returns>
    public static async Task<Client> FindEnabledClientByIdAsync(this IClientStore store, string clientId)
    {
        var client = await store.FindClientByIdAsync(clientId);
        if (client != null && client.Enabled)
        {
            //Telemetry.Metrics.ClientLoaded(client);
            return client;
        }

        return null;
    }
}
