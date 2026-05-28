// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Saml.Endpoints.Results;
using Duende.IdentityServer.Saml.Validation;

namespace Duende.IdentityServer.Saml.ResponseHandling;

/// <summary>
/// Response generator for Saml2 Single Sign On
/// </summary>
public interface ISaml2SsoResponseGenerator
{
    /// <summary>
    /// Create a response for validated AuthnRequest
    /// </summary>
    /// <param name="validatedAuthnRequest">Validated AuthnRequest</param>
    /// <param name="ct">The cancellation token</param>
    /// <returns>Saml2 front channel response</returns>
    Task<Saml2FrontChannelResult> CreateResponse(ValidatedAuthnRequest validatedAuthnRequest, Ct ct);

    /// <summary>
    /// Create an error response for a validated AuthnRequest. If the error is safe to send back
    /// to the SP (as determined by the response generator implementation), returns
    /// a SAML error <c>&lt;Response&gt;</c> via the binding. Otherwise returns an error page redirect.
    /// </summary>
    /// <param name="validatedAuthnRequest">Validated AuthnRequest</param>
    /// <param name="interactionResponse">The interaction error response containing SAML status codes</param>
    /// <param name="ct">The cancellation token</param>
    /// <returns>Saml2 front channel response</returns>
    Task<Saml2FrontChannelResult> CreateErrorResponse(ValidatedAuthnRequest validatedAuthnRequest, Saml2InteractionResponse interactionResponse, Ct ct);
}
