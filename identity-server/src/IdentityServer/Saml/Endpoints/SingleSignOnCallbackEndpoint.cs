// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using System.Net;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Endpoints.Results;
using Duende.IdentityServer.Events;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Hosting;
using Duende.IdentityServer.Saml.Bindings;
using Duende.IdentityServer.Saml.Endpoints.Results;
using Duende.IdentityServer.Saml.Models;
using Duende.IdentityServer.Saml.ResponseHandling;
using Duende.IdentityServer.Saml.Services;
using Duende.IdentityServer.Saml.Validation;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Stores;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
// Alias needed to disambiguate from Duende.IdentityServer.Saml.Models namespace
using InteractionError = Duende.IdentityServer.Models.InteractionError;

namespace Duende.IdentityServer.Saml.Endpoints;

/// <summary>
/// Handles the return from the login page after the user has authenticated during
/// a SAML SSO flow. Retrieves stored SAML state, generates the SAML response,
/// and tracks the SP session.
/// </summary>
internal sealed class SingleSignOnCallbackEndpoint(
    ISamlSigninStateStore stateStore,
    IUserSession userSession,
    ISamlServiceProviderStore serviceProviderStore,
    ISaml2SsoResponseGenerator responseGenerator,
    ISaml2IssuerNameService issuerNameService,
    IServerUrls serverUrls,
    IEventService events,
    IOptions<IdentityServerOptions> identityServerOptions) : IEndpointHandler
{
    private readonly IdentityServerOptions _options = identityServerOptions.Value;
    private readonly SamlEndpointOptions _endpoints = identityServerOptions.Value.Saml.Endpoints;

    /// <inheritdoc/>
    public async Task<IEndpointResult?> ProcessAsync(HttpContext context)
    {
        using var activity = Tracing.BasicActivitySource.StartActivity(IdentityServerConstants.EndpointNames.SamlSingleSignOnCallback + "Endpoint");

        var ct = context.RequestAborted;

        if (!HttpMethods.IsGet(context.Request.Method))
        {
            return new StatusCodeResult(HttpStatusCode.MethodNotAllowed);
        }

        var stateIdStr = context.Request.Query[_endpoints.StateIdParameterName].FirstOrDefault();
        if (stateIdStr == null || !Guid.TryParse(stateIdStr, out var guid))
        {
            return new Saml2FrontChannelResult { Error = "Missing or invalid SAML state identifier" };
        }

        var state = await stateStore.RetrieveSigninRequestStateAsync(guid, ct);
        if (state == null)
        {
            return new Saml2FrontChannelResult { Error = "SAML authentication state not found or expired" };
        }

        // Check if the login page signalled a denial via DenyAuthenticationAsync
        if (state.DenialError is not null)
        {
            return await HandleDenialAsync(state, ct);
        }

        var user = await userSession.GetUserAsync(ct);
        if (user?.Identity?.IsAuthenticated != true)
        {
            return RedirectToLogin(context);
        }

        // If ForceAuthn was requested, verify the user actually re-authenticated
        // after the SAML flow began. This prevents the user from navigating back
        // to the callback URL without re-authenticating.
        if (state.AuthnRequestData is { ForceAuthn: true })
        {
            var authTimeEpoch = user.Identity.GetAuthenticationTimeEpoch();
            var stateCreatedEpoch = state.CreatedUtc.ToUnixTimeSeconds();
            if (authTimeEpoch < stateCreatedEpoch)
            {
                return RedirectToLogin(context);
            }
        }

        var sp = await serviceProviderStore.FindByEntityIdAsync(state.ServiceProviderEntityId, ct);
        if (sp == null)
        {
            await events.RaiseAsync(new SamlSsoFailureEvent(
                state.ServiceProviderEntityId, "Service provider not found", "SingleSignOnCallback"), ct);
            Telemetry.Metrics.SamlSsoFailure(state.ServiceProviderEntityId, "sp_not_found");
            return new Saml2FrontChannelResult
            {
                Error = "Service provider not found",
                SpEntityId = state.ServiceProviderEntityId
            };
        }

        if (!sp.Enabled)
        {
            await events.RaiseAsync(new SamlSsoFailureEvent(
                state.ServiceProviderEntityId, "Service provider is disabled", "SingleSignOnCallback"), ct);
            Telemetry.Metrics.SamlSsoFailure(state.ServiceProviderEntityId, "sp_disabled");
            return new Saml2FrontChannelResult
            {
                Error = "Service provider is disabled",
                SpEntityId = state.ServiceProviderEntityId
            };
        }

        if (!sp.AssertionConsumerServiceUrls.Contains(state.AssertionConsumerService))
        {
            await events.RaiseAsync(new SamlSsoFailureEvent(
                state.ServiceProviderEntityId, "Assertion consumer service URL is no longer registered", "SingleSignOnCallback"), ct);
            Telemetry.Metrics.SamlSsoFailure(state.ServiceProviderEntityId, "invalid_acs_url");
            return new Saml2FrontChannelResult
            {
                Error = "Assertion consumer service URL is no longer registered for this service provider",
                SpEntityId = state.ServiceProviderEntityId
            };
        }

        // State is intentionally not deleted here. The TTL (configured on the store)
        // handles cleanup. Leaving state alive allows the user to retry (e.g., browser
        // reload) if the response doesn't reach the SP on the first attempt.

        var existingSessions = await userSession.GetSamlSessionListAsync(ct);
        var existingSession = existingSessions.FirstOrDefault(s => s.EntityId == sp.EntityId);
        var sessionIndex = existingSession?.SessionIndex ?? Guid.NewGuid().ToString("N");

        var idpEntityId = await issuerNameService.GetCurrentAsync(ct);
        var sid = await userSession.GetSessionIdAsync(ct);

        var validatedAuthnRequest = new ValidatedAuthnRequest
        {
            IdentityServerOptions = _options,
            Binding = state.AssertionConsumerService.Binding.ToUrn(),
            RelayState = state.RelayState,
            Saml2IdpEntityId = idpEntityId,
            Saml2Sp = sp,
            Subject = user,
            SessionId = sid,
            AssertionConsumerService = state.AssertionConsumerService,
            IsIdpInitiated = state.IsIdpInitiated,
            SessionIndex = sessionIndex,
            RequestedClaimTypes = state.RequestedClaimTypes,
            RequestId = state.AuthnRequestData?.RequestId,
            NameIdPolicyFormat = state.AuthnRequestData?.NameIdPolicyFormat
        };

        var response = await responseGenerator.CreateResponse(validatedAuthnRequest, ct);

        if (response.GeneratedNameId != null)
        {
            var sessionData = new SamlSpSessionData
            {
                EntityId = sp.EntityId,
                SessionIndex = sessionIndex,
                NameId = response.GeneratedNameId.Value,
                NameIdFormat = response.GeneratedNameId.Format
            };
            await userSession.AddSamlSessionAsync(sessionData, ct);
        }

        await events.RaiseAsync(new SamlSsoSuccessEvent(
            sp.EntityId,
            user.GetSubjectId(),
            sessionIndex,
            state.AssertionConsumerService.Binding.ToUrn(),
            response.GeneratedNameId?.Format), ct);

        Telemetry.Metrics.SamlSso(sp.EntityId, state.AssertionConsumerService.Binding.ToUrn());

        return response;
    }

    /// <summary>
    /// Handles a denied authentication by generating a SAML error response back to the SP.
    /// </summary>
    private async Task<IEndpointResult> HandleDenialAsync(SamlAuthenticationState state, Ct ct)
    {
        var sp = await serviceProviderStore.FindByEntityIdAsync(state.ServiceProviderEntityId, ct);
        if (sp == null)
        {
            return new Saml2FrontChannelResult
            {
                Error = "Service provider not found",
                SpEntityId = state.ServiceProviderEntityId
            };
        }

        if (!sp.Enabled)
        {
            return new Saml2FrontChannelResult
            {
                Error = "Service provider is disabled",
                SpEntityId = state.ServiceProviderEntityId
            };
        }

        if (!sp.AssertionConsumerServiceUrls.Contains(state.AssertionConsumerService))
        {
            return new Saml2FrontChannelResult
            {
                Error = "Assertion consumer service URL is no longer registered for this service provider",
                SpEntityId = state.ServiceProviderEntityId
            };
        }

        var idpEntityId = await issuerNameService.GetCurrentAsync(ct);

        var validatedAuthnRequest = new ValidatedAuthnRequest
        {
            IdentityServerOptions = _options,
            Binding = state.AssertionConsumerService.Binding.ToUrn(),
            RelayState = state.RelayState,
            Saml2IdpEntityId = idpEntityId,
            Saml2Sp = sp,
            AssertionConsumerService = state.AssertionConsumerService,
            IsIdpInitiated = state.IsIdpInitiated,
            RequestId = state.AuthnRequestData?.RequestId,
            NameIdPolicyFormat = state.AuthnRequestData?.NameIdPolicyFormat
        };

        var (statusCode, subStatusCode) = MapDenialToSamlStatus(state.DenialError!.Value);
        var interactionResponse = state.DenialErrorDescription is not null
            ? Saml2InteractionResponse.Error(statusCode, subStatusCode, state.DenialErrorDescription)
            : Saml2InteractionResponse.Error(statusCode, subStatusCode);

        await events.RaiseAsync(new SamlSsoFailureEvent(
            sp.EntityId, $"access_denied ({state.DenialError.Value})", "SingleSignOnCallback"), ct);
        Telemetry.Metrics.SamlSsoFailure(sp.EntityId, "access_denied");

        return await responseGenerator.CreateErrorResponse(validatedAuthnRequest, interactionResponse, ct);
    }

    /// <summary>
    /// Maps an <see cref="InteractionError"/> to SAML status and sub-status codes.
    /// </summary>
    private static (string StatusCode, string SubStatusCode) MapDenialToSamlStatus(InteractionError error) =>
        error switch
        {
            InteractionError.AccessDenied => (SamlStatusCodes.Responder, SamlStatusCodes.AuthnFailed),
            InteractionError.LoginRequired => (SamlStatusCodes.Responder, SamlStatusCodes.AuthnFailed),
            InteractionError.InteractionRequired => (SamlStatusCodes.Responder, SamlStatusCodes.NoPassive),
            InteractionError.UnmetAuthenticationRequirements => (SamlStatusCodes.Responder, SamlStatusCodes.NoAuthnContext),
            InteractionError.TemporarilyUnavailable => (SamlStatusCodes.Responder, SamlStatusCodes.AuthnFailed),
            InteractionError.ConsentRequired => (SamlStatusCodes.Responder, SamlStatusCodes.RequestDenied),
            InteractionError.AccountSelectionRequired => (SamlStatusCodes.Responder, SamlStatusCodes.AuthnFailed),
            _ => (SamlStatusCodes.Responder, SamlStatusCodes.AuthnFailed)
        };

    /// <summary>
    /// Redirects the user to the login page, preserving the current callback URL
    /// (including the state ID) as the return URL so the flow resumes after authentication.
    /// </summary>
    private Saml2LoginRedirectResult RedirectToLogin(HttpContext context)
    {
        var loginUrl = _options.UserInteraction.LoginUrl
            ?? throw new InvalidOperationException("No login URL configured");
        var returnUrlParam = _options.UserInteraction.LoginReturnUrlParameter
            ?? throw new InvalidOperationException("No login return URL parameter configured");

        var returnUrl = context.Request.Path + context.Request.QueryString;
        if (!loginUrl.IsLocalUrl())
        {
            returnUrl = serverUrls.GetAbsoluteUrl(returnUrl);
        }

        var redirectUrl = loginUrl.AddQueryString(returnUrlParam, returnUrl);

        return new Saml2LoginRedirectResult(redirectUrl);
    }
}
