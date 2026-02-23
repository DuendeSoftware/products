// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Internal.Saml.Infrastructure;
using Duende.IdentityServer.Internal.Saml.SingleSignin.Models;
using Duende.IdentityServer.Saml.Models;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Stores;

namespace Duende.IdentityServer.Internal.Saml.SingleSignin;

internal class SamlSigninCallbackRequestProcessor(
    SamlSigninStateIdCookie stateIdCookie,
    IUserSession userSession,
    ISamlServiceProviderStore serviceProviderStore,
    ISamlSigninStateStore stateStore,
    SamlUrlBuilder samlUrlBuilder,
    SamlResponseBuilder responseBuilder)
{
    internal async Task<Result<SamlSigninSuccess, SamlRequestError<SamlSigninRequest>>> ProcessAsync(CT ct = default)
    {
        if (!stateIdCookie.TryGetSamlSigninStateId(out var stateId))
        {
            return new SamlRequestError<SamlSigninRequest>
            {
                Type = SamlRequestErrorType.Validation,
                ValidationMessage = "No state id could be found."
            };
        }

        var authenticationState = await stateStore.RetrieveSigninRequestStateAsync(stateId.Value, ct);
        if (authenticationState == null)
        {
            return new SamlRequestError<SamlSigninRequest>
            {
                Type = SamlRequestErrorType.Validation,
                ValidationMessage = $"The request {stateId} could not be found."
            };
        }

        var user = await userSession.GetUserAsync();
        if (user == null || !user.IsAuthenticated())
        {
            var loginUri = samlUrlBuilder.SamlLoginUri();

            return SamlSigninSuccess.CreateRedirectSuccess(loginUri);
        }

        var samlServiceProvider =
            await serviceProviderStore.FindByEntityIdAsync(authenticationState.ServiceProviderEntityId);

        if (samlServiceProvider is not { Enabled: true })
        {
            return new SamlRequestError<SamlSigninRequest>
            {
                Type = SamlRequestErrorType.Validation,
                ValidationMessage =
                    $"Service Provider '{authenticationState.ServiceProviderEntityId}' is not registered or is disabled"
            };
        }

        // Check if this SP already has a session - if so, reuse the SessionIndex
        var existingSessions = await userSession.GetSamlSessionListAsync();
        var existingSession = existingSessions.FirstOrDefault(s => s.EntityId == samlServiceProvider.EntityId);
        string sessionIndex;

        if (existingSession != null)
        {
            // Reuse existing SessionIndex (e.g., for step-up authentication)
            sessionIndex = existingSession.SessionIndex;
        }
        else
        {
            // Generate new SessionIndex for this SP
            sessionIndex = Guid.NewGuid().ToString("N");
        }

        var samlResponse = await responseBuilder.BuildSuccessResponseAsync(user, samlServiceProvider, authenticationState, sessionIndex);

        if (string.IsNullOrEmpty(samlResponse.Assertion?.Subject?.NameId?.Value))
        {
            throw new InvalidOperationException("SAML success response created without a NameId value");
        }

        if (string.IsNullOrEmpty(samlResponse.Assertion?.Subject?.NameId?.Format))
        {
            throw new InvalidOperationException("SAML success response created without a NameId format");
        }

        // Track the SAML SP session for logout coordination
        var sessionData = new SamlSpSessionData
        {
            EntityId = samlServiceProvider.EntityId,
            SessionIndex = sessionIndex,
            NameId = samlResponse.Assertion.Subject.NameId.Value,
            NameIdFormat = samlResponse.Assertion.Subject.NameId.Format
        };
        await userSession.AddSamlSessionAsync(sessionData);

        stateIdCookie.ClearAuthenticationState();

        return SamlSigninSuccess.CreateResponseSuccess(samlResponse);
    }
}
