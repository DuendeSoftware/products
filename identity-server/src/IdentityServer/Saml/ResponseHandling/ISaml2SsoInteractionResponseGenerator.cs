// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Saml.Validation;

namespace Duende.IdentityServer.Saml.ResponseHandling;

/// <summary>
/// Interaction response generator for Saml2 AuthnRequests
/// </summary>
public interface ISaml2SsoInteractionResponseGenerator
{
    /// <summary>
    /// Process a validated AuthnRequest and decide what/if interaction is required.
    /// </summary>
    /// <param name="request">Validated AuthnRequest</param>
    /// <param name="ct">The cancellation token</param>
    /// <returns>Interaction</returns>
    Task<Saml2InteractionResponse> ProcessInteractionAsync(ValidatedAuthnRequest request, Ct ct);
}
