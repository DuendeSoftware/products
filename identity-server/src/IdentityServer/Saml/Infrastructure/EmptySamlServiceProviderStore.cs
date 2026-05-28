// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Runtime.CompilerServices;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;

namespace Duende.IdentityServer.Saml.Infrastructure;

internal class EmptySamlServiceProviderStore : ISamlServiceProviderStore
{
    public Task<SamlServiceProvider> FindByEntityIdAsync(string entityId, Ct ct) => Task.FromResult<SamlServiceProvider>(null);

    public async IAsyncEnumerable<SamlServiceProvider> GetAllSamlServiceProvidersAsync([EnumeratorCancellation] Ct ct)
    {
        await Task.CompletedTask;
        yield break;
    }
}
