// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;

namespace Duende.IdentityServer.Internal.Saml;

internal class EmptySamlServiceProviderStore : ISamlServiceProviderStore
{
    public Task<SamlServiceProvider> FindByEntityIdAsync(string entityId) => Task.FromResult<SamlServiceProvider>(null);
}
