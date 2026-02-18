// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Saml.Models;

namespace Duende.IdentityServer.Saml;

/// <summary>
/// Service for customizing how claims are mapped to SAML attributes.
/// If registered, this service completely replaces the default mapping logic.
/// </summary>
public interface ISamlClaimsMapper
{
    /// <summary>
    /// Maps claims to SAML attributes.
    ///
    /// This method is called when a custom mapper is registered and completely
    /// replaces the default mapping behavior (global + service provider mappings).
    /// </summary>
    /// <param name="claimsMappingContext">Context with information about the authentication request for which claims need to be mapped</param>
    /// <returns>The mapped SAML attributes</returns>
    Task<IEnumerable<SamlAttribute>> MapClaimsAsync(SamlClaimsMappingContext claimsMappingContext);
}
