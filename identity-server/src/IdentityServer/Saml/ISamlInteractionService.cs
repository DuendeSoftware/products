// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using Duende.IdentityServer.Saml.Models;

namespace Duende.IdentityServer.Saml;

/// <summary>
/// Provide services to be used by the user interface to communicate with IdentityServer for SAML flows.
/// </summary>
public interface ISamlInteractionService
{
    /// <summary>
    /// Gets the SAML authentication request context from the current request's state cookie.
    /// Returns null if no SAML authentication is in progress.
    /// </summary>
    Task<SamlAuthenticationRequest?> GetAuthenticationRequestContextAsync(CT ct = default);

    /// <summary>
    /// Stores whether the user met the requirements of the RequestedAuthnContext in the
    /// AuthNRequest. If the value is set to false, the generated response will include a second-level
    /// status code of urn:oasis:names:tc:SAML:2.0:status:NoAuthnContext per section 3.3.2.2.1 of the
    /// core spec.
    /// </summary>
    /// <param name="requestedAuthnContextRequirementsWereMet">Whether the requirements of the RequestedAuthnContext were met.</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns></returns>
    Task StoreRequestedAuthnContextResultAsync(bool requestedAuthnContextRequirementsWereMet, CT ct = default);
}
