// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.Stores;

/// <summary>
/// Persists and retrieves identity provider configuration used for dynamic external
/// authentication. IdentityServer uses this store to dynamically load
/// <see cref="IdentityProvider"/> configuration at runtime, enabling external identity
/// providers (such as OpenID Connect providers via <c>OidcProvider</c>) to be managed
/// without restarting the server. Implement this interface to load identity provider
/// configuration from any backing store.
/// </summary>
public interface IIdentityProviderStore
{
    /// <summary>
    /// Gets the display names and scheme names of all registered identity providers.
    /// This lightweight projection is used to populate login UI elements such as
    /// external login buttons without loading full provider configuration.
    /// </summary>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>
    /// A read-only collection of <see cref="IdentityProviderName"/> objects, each
    /// containing the scheme name and display name of a registered identity provider.
    /// Returns an empty collection when no providers are registered.
    /// </returns>
    Task<IReadOnlyCollection<IdentityProviderName>> GetAllSchemeNamesAsync(Ct ct);

    /// <summary>
    /// Gets the full identity provider configuration for the specified authentication
    /// scheme name.
    /// </summary>
    /// <param name="scheme">The authentication scheme name to look up.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>
    /// The <see cref="IdentityProvider"/> registered under <paramref name="scheme"/>,
    /// or <see langword="null"/> if no matching provider exists. For OpenID Connect
    /// providers the returned instance will be an <c>OidcProvider</c>.
    /// </returns>
    Task<IdentityProvider?> GetBySchemeAsync(string scheme, Ct ct);
}
