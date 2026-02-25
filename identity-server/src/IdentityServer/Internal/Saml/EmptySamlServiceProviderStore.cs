// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using System.Runtime.CompilerServices;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;

namespace Duende.IdentityServer.Internal.Saml;

internal class EmptySamlServiceProviderStore : ISamlServiceProviderStore
{
    public Task<SamlServiceProvider?> FindByEntityIdAsync(string entityId, Ct _) => Task.FromResult<SamlServiceProvider?>(null);

#if NET10_0_OR_GREATER
    public async IAsyncEnumerable<SamlServiceProvider> GetAllSamlServiceProvidersAsync([EnumeratorCancellation] Ct _)
    {
        await Task.CompletedTask;
        yield break;
    }
#endif
}
