// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.Stores;

/// <summary>
/// In-memory SAML Service Provider store.
/// </summary>
public class InMemorySamlServiceProviderStore : ISamlServiceProviderStore
{
    private readonly IEnumerable<SamlServiceProvider> _serviceProviders;

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemorySamlServiceProviderStore"/> class.
    /// </summary>
    /// <param name="serviceProviders">The service providers.</param>
    public InMemorySamlServiceProviderStore(IEnumerable<SamlServiceProvider> serviceProviders)
    {
        if (serviceProviders.HasDuplicates(m => m.EntityId))
        {
            throw new ArgumentException("Service providers must not contain duplicate entity IDs");
        }
        _serviceProviders = serviceProviders;
    }

    /// <summary>
    /// Finds a SAML Service Provider by its entity identifier.
    /// </summary>
    /// <param name="entityId">The entity identifier of the Service Provider.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The Service Provider, or null if not found.</returns>
    public Task<SamlServiceProvider> FindByEntityIdAsync(string entityId, Ct ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("InMemorySamlServiceProviderStore.FindByEntityId");
        activity?.SetTag(Tracing.Properties.SamlEntityId, entityId);

        var query =
            from sp in _serviceProviders
            where sp.EntityId == entityId && sp.Enabled
            select sp;

        return Task.FromResult(query.SingleOrDefault());
    }
}
