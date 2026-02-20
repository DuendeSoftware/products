// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Saml;
using Duende.IdentityServer.Saml.Models;
using Duende.IdentityServer.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Internal.Saml.SingleSignin;

internal class DefaultSamlSigninInteractionResponseGenerator(
    IUserSession userSession,
    ILogger<DefaultSamlSigninInteractionResponseGenerator> logger,
    IHttpContextAccessor httpContextAccessor)
    : ISamlSigninInteractionResponseGenerator
{
    public async Task<SamlInteractionResponse> ProcessInteractionAsync(SamlServiceProvider sp, AuthNRequest request, CancellationToken ct)
    {
        var signedInUser = await userSession.GetUserAsync();

        if (signedInUser != null)
        {
            if (request.IsPassive && request.ForceAuthn)
            {
                // Below is quite ambiguous in the spec. IsPassive means no user interaction. But ForceAuthn means we must re-authenticate.
                // For now, we have no way to re-authenticate the user without user interaction.

                // From the spec:
                //ForceAuthn[Optional]
                //A Boolean value.If "true", the identity provider MUST authenticate the presenter directly rather than
                //rely on a previous security context. If a value is not provided, the default is "false".However, if both
                //  ForceAuthn and IsPassive are "true", the identity provider MUST NOT freshly authenticate the
                //presenter unless the constraints of IsPassive can be met.
                logger.SamlInteractionPassiveAndForced(LogLevel.Debug);
                return SamlInteractionResponse.CreateError(SamlStatusCodes.NoPassive, "The user is not currently logged in");
            }

            if (request.ForceAuthn)
            {
                logger.SamlInteractionForced(LogLevel.Debug);

                ArgumentNullException.ThrowIfNull(httpContextAccessor.HttpContext, nameof(httpContextAccessor.HttpContext));
                await httpContextAccessor.HttpContext.SignOutAsync();

                return SamlInteractionResponse.Create(SamlInteractionResponseType.Login);
            }

            logger.SamlInteractionAlreadyAuthenticated(LogLevel.Debug);
            return SamlInteractionResponse.Create(SamlInteractionResponseType.AlreadyAuthenticated);
        }

        if (request.IsPassive)
        {
            logger.SamlInteractionNoPassive(LogLevel.Debug);
            return SamlInteractionResponse.CreateError(SamlStatusCodes.NoPassive, "The user is not currently logged in and passive login was requested.");
        }

        // Todo: The AuthN request may contain hints on account creation 3.4.1.1 Element <NameIDPolicy>: AllowCreate


        // Consent is a weird one.
        // There is no way for SAML for an SP to mandate that a consent screen should be shown.
        if (sp.RequireConsent && !IsConsentAcquired(request.Consent))
        {
            logger.SamlInteractionConsent(LogLevel.Debug);
            return SamlInteractionResponse.Create(SamlInteractionResponseType.Consent);
        }

        logger.SamlInteractionLogin(LogLevel.Debug);
        return SamlInteractionResponse.Create(SamlInteractionResponseType.Login);
    }

    /// <summary>
    /// Determines whether consent has been acquired based on the SAML consent URN value.
    /// See SAML 2.0 Core spec section 8.4.
    /// </summary>
    private static bool IsConsentAcquired(string? consent) => consent is
        "urn:oasis:names:tc:SAML:2.0:consent:obtained" or
        "urn:oasis:names:tc:SAML:2.0:consent:prior" or
        "urn:oasis:names:tc:SAML:2.0:consent:current-implicit" or
        "urn:oasis:names:tc:SAML:2.0:consent:current-explicit";
}
