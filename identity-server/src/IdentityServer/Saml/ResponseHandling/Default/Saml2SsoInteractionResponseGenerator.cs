// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Saml.Models;
using Duende.IdentityServer.Saml.Validation;
using Duende.IdentityServer.Services;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Saml.ResponseHandling;

/// <summary>
/// Determines whether user interaction (login) is required for a SAML SSO request.
/// This generator is only invoked on the SSO endpoint path (initial AuthnRequest processing),
/// where <see cref="ValidatedAuthnRequest.AuthnRequest"/> is always populated from the parsed XML.
/// It is never called on the callback path after login.
/// </summary>
public sealed class Saml2SsoInteractionResponseGenerator(
    IProfileService profileService,
    ILogger<Saml2SsoInteractionResponseGenerator> logger) : ISaml2SsoInteractionResponseGenerator
{
    /// <inheritdoc/>
    public async Task<Saml2InteractionResponse> ProcessInteractionAsync(ValidatedAuthnRequest request, Ct ct)
    {
        if (request.Subject == null)
        {
            if (request.AuthnRequest is { IsPassive: true })
            {
                return Saml2InteractionResponse.Error(
                    SamlStatusCodes.Responder,
                    SamlStatusCodes.NoPassive,
                    "Cannot passively authenticate user");
            }

            return Saml2InteractionResponse.Login();
        }

        // Check if the user is authenticated and still active
        var isAuthenticated = request.Subject.IsAuthenticated();
        var isActive = false;

        if (isAuthenticated && request.Saml2Sp != null)
        {
            var isActiveCtx = new IsActiveContext(request.Subject, request.Saml2Sp, IdentityServerConstants.ProfileIsActiveCallers.SamlSsoEndpoint);
            await profileService.IsActiveAsync(isActiveCtx, ct);
            isActive = isActiveCtx.IsActive;
        }

        if (!isAuthenticated || !isActive)
        {
            if (!isAuthenticated)
            {
                logger.ShowingLoginUserNotAuthenticated(LogLevel.Information);
            }
            else if (request.Saml2Sp == null)
            {
                logger.ShowingLoginServiceProviderContextMissing();
            }
            else
            {
                logger.ShowingLoginUserNotActive(LogLevel.Information);
            }

            if (request.AuthnRequest is { IsPassive: true })
            {
                return Saml2InteractionResponse.Error(
                    SamlStatusCodes.Responder,
                    SamlStatusCodes.NoPassive,
                    "Cannot passively authenticate user");
            }

            return Saml2InteractionResponse.Login();
        }

        if (request.AuthnRequest is { ForceAuthn: true })
        {
            if (request.AuthnRequest.IsPassive)
            {
                return Saml2InteractionResponse.Error(
                    SamlStatusCodes.Responder,
                    SamlStatusCodes.NoPassive,
                    "Cannot passively authenticate user when force auth is required");
            }

            return Saml2InteractionResponse.Login();
        }

        return Saml2InteractionResponse.NoInteraction();
    }
}
