// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Endpoints.Results;
using Duende.IdentityServer.Events;
using Duende.IdentityServer.Hosting;
using Duende.IdentityServer.Saml.Bindings;
using Duende.IdentityServer.Saml.Endpoints.Results;
using Duende.IdentityServer.Saml.ResponseHandling;
using Duende.IdentityServer.Saml.Serialization;
using Duende.IdentityServer.Saml.Services;
using Duende.IdentityServer.Saml.Validation;
using Duende.IdentityServer.Saml.Xml;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Stores;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Saml.Endpoints;

internal sealed class SingleLogoutServiceEndpoint(
    IEnumerable<IFrontChannelBinding> frontChannelBindings,
    ServiceProviderEntityResolver serviceProviderEntityResolver,
    ISamlXmlReader samlXmlReader,
    IUserSession userSession,
    ILogoutRequestValidator logoutRequestValidator,
    ISaml2IssuerNameService saml2IssuerNameService,
    ISaml2SloResponseGenerator responseGenerator,
    ISamlLogoutSessionStore samlLogoutSessionStore,
    IEventService events,
    IdentityServerOptions options,
    ISamlServiceProviderStore serviceProviderStore,
    ILogger<SingleLogoutServiceEndpoint> logger) : IEndpointHandler
{
    public async Task<IEndpointResult?> ProcessAsync(HttpContext context)
    {
        using var activity = Tracing.BasicActivitySource.StartActivity(IdentityServerConstants.EndpointNames.SamlSingleLogoutService + "Endpoint");

        if (!HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsPost(context.Request.Method))
        {
            return new Saml2FrontChannelResult
            {
                Error = "Method not allowed"
            };
        }

        var binding = frontChannelBindings.FirstOrDefault(b => b.CanUnBind(context.Request));
        if (binding == null)
        {
            return new Saml2FrontChannelResult
            {
                Error = "No front channel binding found to satisfy request"
            };
        }

        InboundSaml2Message message;
        try
        {
            message = await binding.UnBindAsync(context.Request, serviceProviderEntityResolver.ResolveAsync);
        }
        catch (FormatException)
        {
            return new Saml2FrontChannelResult
            {
                Error = "Invalid base64 encoding in SAML logout message"
            };
        }

        return message.Name switch
        {
            SamlConstants.RequestProperties.SAMLRequest => await ProcessLogoutRequestAsync(message, binding, context),
            SamlConstants.RequestProperties.SAMLResponse => await ProcessLogoutResponseAsync(message, context),
            _ => new Saml2FrontChannelResult { Error = $"Unexpected SAML message type: {message.Name}" }
        };
    }

    private async Task<IEndpointResult?> ProcessLogoutRequestAsync(InboundSaml2Message message, IFrontChannelBinding binding, HttpContext context)
    {
        Samlp.LogoutRequest logoutRequest;
        try
        {
            var traverser = new XmlTraverser(message.Xml, message.TrustLevel);
            logoutRequest = await samlXmlReader.ReadLogoutRequestAsync(
                traverser, options.Saml.LogoutRequestErrorInspector, context.RequestAborted);
        }
        catch (Exception e) when (e is SamlXmlException or InvalidOperationException)
        {
            logger.SamlParsingError(e);
            return new Saml2FrontChannelResult
            {
                Error = "The SAML logout request could not be processed"
            };
        }

        var user = await userSession.GetUserAsync(context.RequestAborted);
        var sid = await userSession.GetSessionIdAsync(context.RequestAborted);
        var idpEntityId = await saml2IssuerNameService.GetCurrentAsync(context.RequestAborted);

        var validatedRequest = new ValidatedLogoutRequest
        {
            LogoutRequest = logoutRequest,
            Binding = binding.Identifier,
            Saml2Message = message,
            Subject = user,
            SessionId = sid,
            Saml2IdpEntityId = idpEntityId
        };

        var validationResult = await logoutRequestValidator.ValidateAsync(validatedRequest, context.RequestAborted);

        if (validationResult.IsError)
        {
            await events.RaiseAsync(new SamlLogoutRequestValidationFailureEvent(
                logoutRequest.Issuer?.Value, validationResult.ErrorDescription ?? "Logout request validation failed", binding.Identifier), context.RequestAborted);
            // Use the resolved SP entity ID only if validation confirmed the SP exists in
            // configuration. The raw Issuer from the request is attacker-controlled and could
            // cause unbounded metric cardinality if used directly as a tag value.
            var resolvedSpEntityId = validationResult.ValidatedRequest.Saml2Sp?.EntityId;
            Telemetry.Metrics.SamlSloFailure(
                resolvedSpEntityId ?? "invalid",
                validationResult.Error ?? "unknown");
            return new Saml2FrontChannelResult
            {
                Error = validationResult.ErrorDescription,
                SpEntityId = logoutRequest.Issuer?.Value
            };
        }

        // Attach the resolved SP from validation
        validatedRequest.Saml2Sp = validationResult.ValidatedRequest.Saml2Sp;

        // If no user is authenticated or no matching SAML session was found for this SP,
        // return success immediately without terminating the IdP session (per SAML 2.0 Profiles §4.4).
        if (user == null || !validationResult.SessionFound)
        {
            await events.RaiseAsync(new SamlSloSuccessEvent(
                validatedRequest.Saml2Sp!.EntityId,
                logoutRequest.SessionIndex,
                "SP"), context.RequestAborted);
            Telemetry.Metrics.SamlSlo(validatedRequest.Saml2Sp!.EntityId);
            return await responseGenerator.CreateSuccessResponse(validatedRequest, context.RequestAborted);
        }

        // User is authenticated — redirect to logout page; callback will send the LogoutResponse
        return new Saml2LogoutPageResult(validatedRequest);
    }

    private async Task<IEndpointResult?> ProcessLogoutResponseAsync(InboundSaml2Message message, HttpContext context)
    {
        Samlp.LogoutResponse logoutResponse;
        try
        {
            var traverser = new XmlTraverser(message.Xml, message.TrustLevel);
            logoutResponse = await samlXmlReader.ReadLogoutResponseAsync(
                traverser, options.Saml.LogoutResponseErrorInspector, context.RequestAborted);
        }
        catch (Exception e) when (e is SamlXmlException or InvalidOperationException)
        {
            logger.FailedToParseSamlLogoutResponse(e);
            return new StatusCodeResult(System.Net.HttpStatusCode.OK);
        }

        var statusCode = logoutResponse.Status?.StatusCode?.Value;
        var subStatusCode = logoutResponse.Status?.StatusCode?.NestedStatusCode?.Value;

        logger.ReceivedSamlLogoutResponse(LogLevel.Debug, logoutResponse.Issuer?.Value, statusCode, logoutResponse.InResponseTo);

        if (statusCode != null && statusCode != Models.SamlStatusCodes.Success)
        {
            logger.SpReportedNonSuccessLogoutStatus(LogLevel.Warning, logoutResponse.Issuer?.Value, statusCode);
        }

        var inResponseTo = logoutResponse.InResponseTo;
        var issuer = logoutResponse.Issuer?.Value;
        if (string.IsNullOrEmpty(inResponseTo) || string.IsNullOrEmpty(issuer))
        {
            logger.SamlLogoutResponseMissingInResponseToOrIssuer(LogLevel.Debug);
            return new StatusCodeResult(System.Net.HttpStatusCode.OK);
        }

        // Use the effective trust level: XML signature sets it on the response, but
        // redirect-binding signatures set it on the inbound message. Take the higher of the two.
        var effectiveTrustLevel = logoutResponse.TrustLevel > message.TrustLevel
            ? logoutResponse.TrustLevel
            : message.TrustLevel;

        if (!await ValidateResponseTrustAsync(issuer, effectiveTrustLevel, context.RequestAborted))
        {
            return new StatusCodeResult(System.Net.HttpStatusCode.OK);
        }

        await RecordLogoutResponseAsync(inResponseTo, issuer, statusCode, subStatusCode, context.RequestAborted);

        return new StatusCodeResult(System.Net.HttpStatusCode.OK);
    }

    /// <summary>
    /// Validates that the LogoutResponse meets the trust requirements for the given SP.
    /// Returns <c>true</c> if the response should be recorded, <c>false</c> if it should be rejected.
    /// </summary>
    private async Task<bool> ValidateResponseTrustAsync(string issuer, TrustLevel trustLevel, Ct ct)
    {
        var sp = await serviceProviderStore.FindByEntityIdAsync(issuer, ct);

        if (sp == null)
        {
            logger.NoServiceProviderConfigurationFoundForIssuer(LogLevel.Warning, issuer);
        }

        var requireSigned = sp?.RequireSignedLogoutResponses ?? options.Saml.RequireSignedLogoutResponses;

        if (requireSigned && trustLevel < TrustLevel.TLS)
        {
            logger.RejectingUnsignedSamlLogoutResponse(LogLevel.Warning, issuer, trustLevel);
            return false;
        }

        if (!requireSigned && trustLevel < TrustLevel.TLS)
        {
            logger.AcceptingUnsignedSamlLogoutResponse(LogLevel.Warning, issuer);
        }

        return true;
    }

    /// <summary>
    /// Records the LogoutResponse outcome in the session store for SLO completion tracking.
    /// </summary>
    private async Task RecordLogoutResponseAsync(
        string inResponseTo, string issuer, string? statusCode, string? subStatusCode, Ct ct)
    {
        // PartialLogout sub-status means not all sessions were terminated — treat as non-success
        var isSuccess = statusCode == Models.SamlStatusCodes.Success
            && subStatusCode != Models.SamlStatusCodes.PartialLogout;

        var recorded = await samlLogoutSessionStore.TryRecordResponseAsync(inResponseTo, issuer, isSuccess, ct);
        if (!recorded)
        {
            logger.FailedToRecordSamlLogoutResponse(LogLevel.Warning, inResponseTo, issuer);
        }
    }
}
