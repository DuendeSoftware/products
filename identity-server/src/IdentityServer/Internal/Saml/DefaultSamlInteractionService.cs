// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using Duende.IdentityServer.Internal.Saml.SingleSignin;
using Duende.IdentityServer.Saml;
using Duende.IdentityServer.Saml.Models;
using Duende.IdentityServer.Stores;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Internal.Saml;

internal class DefaultSamlInteractionService(
    ISamlSigninStateStore stateStore,
    SamlSigninStateIdCookie stateIdCookie,
    ISamlServiceProviderStore serviceProviderStore,
    ILogger<DefaultSamlInteractionService> logger)
    : ISamlInteractionService
{
    public async Task<SamlAuthenticationRequest?> GetAuthenticationRequestContextAsync(Ct ct = default)
    {
        using var activity = Tracing.ServiceActivitySource.StartActivity("DefaultSamlInteractionService.GetAuthenticationRequestContext");

        if (!stateIdCookie.TryGetSamlSigninStateId(out var stateId))
        {
            logger.NoSamlAuthenticationStateFound(LogLevel.Warning);
            return null;
        }

        var state = await stateStore.RetrieveSigninRequestStateAsync(stateId.Value, ct);
        if (state == null)
        {
            logger.StateNotFound(LogLevel.Warning, stateId.Value);
            return null;
        }

        var sp = await serviceProviderStore.FindByEntityIdAsync(state.ServiceProviderEntityId, ct);
        if (sp == null)
        {
            logger.ServiceProviderNotFound(LogLevel.Warning, state.ServiceProviderEntityId);
            return null;
        }

        logger.AuthenticationStateLoaded(LogLevel.Debug, sp.EntityId);

        return new SamlAuthenticationRequest
        {
            ServiceProvider = sp,
            AuthNRequest = state.Request,
            RelayState = state.RelayState,
            IsIdpInitiated = state.IsIdpInitiated
        };
    }

    public async Task StoreRequestedAuthnContextResultAsync(bool requestedAuthnContextRequirementsWereMet, Ct ct = default)
    {
        using var activity = Tracing.ServiceActivitySource.StartActivity("DefaultSamlInteractionService.StoreRequestedAuthnContextResult");

        if (!stateIdCookie.TryGetSamlSigninStateId(out var stateId))
        {
            logger.NoSamlAuthenticationStateFound(LogLevel.Warning);
            throw new InvalidOperationException("No active SAML authentication request found. Cannot store authentication error.");
        }

        var state = await stateStore.RetrieveSigninRequestStateAsync(stateId.Value, ct);
        if (state == null)
        {
            logger.StateNotFound(LogLevel.Warning, stateId.Value);
            throw new InvalidOperationException($"SAML signin state not found for state ID {stateId.Value}");
        }

        state.RequestedAuthnContextRequirementsWereMet = requestedAuthnContextRequirementsWereMet;
        await stateStore.UpdateSigninRequestStateAsync(stateId.Value, state, ct);

        logger.RequestedAuthnContextRequirementsWereMetUpdatedInState(LogLevel.Debug, requestedAuthnContextRequirementsWereMet);
    }
}
