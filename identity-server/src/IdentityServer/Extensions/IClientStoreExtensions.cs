// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.Stores;

/// <summary>
/// Extension for IClientStore
/// </summary>
public static class IClientStoreExtensions
{
    extension(IClientStore store)
    {
        /// <summary>
        /// Finds the enabled client by identifier.
        /// </summary>
        /// <param name="clientId">The client identifier.</param>
        /// <param name="ct">The cancellation token.</param>
        /// <returns></returns>
        public async Task<Client> FindEnabledClientByIdAsync(string clientId, Ct ct)
        {
            var client = await store.FindClientByIdAsync(clientId, ct);
            if (client != null && client.Enabled)
            {
                return client;
            }

            return null;
        }
    }
}
