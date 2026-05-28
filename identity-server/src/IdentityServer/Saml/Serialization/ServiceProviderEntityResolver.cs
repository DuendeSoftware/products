// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Saml.Xml;
using Duende.IdentityServer.Stores;

namespace Duende.IdentityServer.Saml.Serialization;

/// <summary>
/// Resolves a <see cref="Saml2Entity"/> from <see cref="ISamlServiceProviderStore"/> for use
/// as the <see cref="ISamlXmlReader.EntityResolver"/> on the IdP side.
/// </summary>
public sealed class ServiceProviderEntityResolver
{
    private readonly ISamlServiceProviderStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceProviderEntityResolver"/> class.
    /// </summary>
    /// <param name="store">The service provider store.</param>
    public ServiceProviderEntityResolver(ISamlServiceProviderStore store) => _store = store;

    /// <summary>
    /// Resolves a <see cref="Saml2Entity"/> for the given entity ID by looking up the
    /// corresponding service provider and mapping its signing certificates.
    /// Returns <c>null</c> when the SP is not found or has no signing certificates.
    /// </summary>
    /// <param name="entityId">The entity ID to resolve.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<Saml2Entity?> ResolveAsync(string entityId, Ct ct)
    {
        var serviceProvider = await _store.FindByEntityIdAsync(entityId, ct);

        var signingCerts = serviceProvider?.Certificates?
            .Where(c => c.Use.HasFlag(KeyUse.Signing))
            .Select(c => c.Certificate)
            .ToList();

        if (signingCerts is not { Count: > 0 })
        {
            return null;
        }

        var entity = new Saml2Entity
        {
            EntityId = entityId,
            SigningKeys = signingCerts.Select(c => new SigningKey { Certificate = c }),
        };

        // Only override if the SP has an explicit allowlist; otherwise the
        // Saml2Entity property initializer provides secure global defaults.
        if (serviceProvider?.AllowedSignatureAlgorithms is { Count: > 0 } algorithms)
        {
            entity.AllowedAlgorithms = algorithms;
        }

        return entity;
    }
}
