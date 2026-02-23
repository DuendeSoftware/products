// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Internal.Saml.Infrastructure;
using Duende.IdentityServer.Internal.Saml.SingleSignin.Models;
using Duende.IdentityServer.Stores;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Duende.IdentityServer.Internal.Saml.SingleSignin;

internal class SamlIdpInitiatedRequestProcessor(
    ISamlServiceProviderStore serviceProviderStore,
    ISamlSigninStateStore stateStore,
    SamlUrlBuilder samlUrlBuilder,
    SamlSigninStateIdCookie stateIdCookie,
    IHttpContextAccessor httpContextAccessor,
    IOptions<SamlOptions> options)
{
    private readonly SamlOptions _samlOptions = options.Value;

    internal async Task<Result<SamlSigninSuccess, SamlRequestError<SamlSigninRequest>>> ProcessAsync(
        string spEntityId,
        string? relayState,
        CT ct = default)
    {
        var sp = await serviceProviderStore.FindByEntityIdAsync(spEntityId);
        if (sp == null)
        {
            return new SamlRequestError<SamlSigninRequest>
            {
                Type = SamlRequestErrorType.Validation,
                ValidationMessage = $"Service Provider '{spEntityId}' is not registered"
            };
        }

        if (!sp.Enabled)
        {
            return new SamlRequestError<SamlSigninRequest>
            {
                Type = SamlRequestErrorType.Validation,
                ValidationMessage = $"Service Provider '{spEntityId}' is disabled"
            };
        }

        if (!sp.AllowIdpInitiated)
        {
            return new SamlRequestError<SamlSigninRequest>
            {
                Type = SamlRequestErrorType.Validation,
                ValidationMessage = $"Service Provider '{spEntityId}' does not allow IdP-initiated SSO"
            };
        }

        if (relayState != null)
        {
            var relayStateBytes = System.Text.Encoding.UTF8.GetByteCount(relayState);
            if (relayStateBytes > _samlOptions.MaxRelayStateLength)
            {
                return new SamlRequestError<SamlSigninRequest>
                {
                    Type = SamlRequestErrorType.Validation,
                    ValidationMessage = $"RelayState exceeds maximum length of {_samlOptions.MaxRelayStateLength} bytes"
                };
            }
        }

        var acsUrl = sp.AssertionConsumerServiceUrls.FirstOrDefault();
        if (acsUrl == null)
        {
            return new SamlRequestError<SamlSigninRequest>
            {
                Type = SamlRequestErrorType.Validation,
                ValidationMessage = $"Service Provider '{spEntityId}' has no AssertionConsumerServiceUrls configured"
            };
        }

        string? relayStateParam = null;
        if (!string.IsNullOrEmpty(relayState))
        {
            relayStateParam = relayState;
        }

        var state = new SamlAuthenticationState
        {
            Request = null, // No AuthNRequest for IdP-initiated
            RelayState = relayStateParam,
            ServiceProviderEntityId = sp.EntityId,
            AssertionConsumerServiceUrl = acsUrl,
            IsIdpInitiated = true,
            CreatedUtc = DateTimeOffset.UtcNow
        };

        var storedStateId = await stateStore.StoreSigninRequestStateAsync(state, ct);
        stateIdCookie.StoreSamlSigninStateId(storedStateId);

        // Determine redirect based on authentication status
        var httpContext = httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("No HttpContext available");

        var isAuthenticated = httpContext.User.Identity?.IsAuthenticated ?? false;

        Uri redirectUrl;
        if (isAuthenticated)
        {
            redirectUrl = samlUrlBuilder.SamlSignInCallBackUri();
        }
        else
        {
            redirectUrl = samlUrlBuilder.SamlLoginUri();
        }

        return SamlSigninSuccess.CreateRedirectSuccess(redirectUrl);
    }
}
