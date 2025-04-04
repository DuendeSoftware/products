// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.Stores.Empty;

internal class EmptyClientStore : IClientStore
{
    public Task<Client> FindClientByIdAsync(string clientId) => Task.FromResult<Client>(null);
}
