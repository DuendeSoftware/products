// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using System.Net;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Endpoints.Results;
using Duende.IdentityServer.Events;
using Duende.IdentityServer.Hosting;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Saml.Bindings;
using Duende.IdentityServer.Saml.Endpoints.Results;
using Duende.IdentityServer.Saml.ResponseHandling;
using Duende.IdentityServer.Saml.Services;
using Duende.IdentityServer.Saml.Validation;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Stores;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using SamlLogoutRequest = Duende.IdentityServer.Saml.Samlp.LogoutRequest;

namespace Duende.IdentityServer.Saml.Endpoints;

internal sealed class SingleLogoutCallbackEndpoint(
    IdentityServerOptions options,
    IMessageStore<LogoutMessage> logoutMessageStore,
    ISamlServiceProviderStore serviceProviderStore,
    ISaml2SloResponseGenerator responseGenerator,
    ISaml2IssuerNameService issuerNameService,
    ISamlLogoutSessionStore samlLogoutSessionStore,
    IEventService events,
    ILogger<SingleLogoutCallbackEndpoint> logger) : IEndpointHandler
{
    public async Task<IEndpointResult?> ProcessAsync(HttpContext context)
    {
        using var activity = Tracing.BasicActivitySource.StartActivity(IdentityServerConstants.EndpointNames.SamlSingleLogoutCallback + "Endpoint");

        if (!HttpMethods.IsGet(context.Request.Method))
        {
            return new StatusCodeResult(HttpStatusCode.MethodNotAllowed);
        }

        var logoutId = context.Request.Query[options.UserInteraction.LogoutIdParameter].ToString();
        if (string.IsNullOrWhiteSpace(logoutId))
        {
            logger.MissingLogoutIdParameter(LogLevel.Warning);
            return new Saml2FrontChannelResult { Error = "Missing or invalid SAML logout state identifier" };
        }

        var logoutMessage = await logoutMessageStore.ReadAsync(logoutId, context.RequestAborted);
        if (logoutMessage?.Data == null)
        {
            logger.NoLogoutMessageFound(LogLevel.Warning, logoutId);
            return new Saml2FrontChannelResult { Error = "SAML logout state not found or expired" };
        }

        var data = logoutMessage.Data;

        if (string.IsNullOrWhiteSpace(data.SamlServiceProviderEntityId))
        {
            logger.LogoutMessageMissingEntityId(LogLevel.Warning);
            return new Saml2FrontChannelResult { Error = "SAML logout state is missing service provider information" };
        }

        if (string.IsNullOrWhiteSpace(data.SamlLogoutRequestId))
        {
            logger.LogoutMessageMissingRequestId(LogLevel.Warning);
            return new Saml2FrontChannelResult { Error = "SAML logout state is missing request identifier" };
        }

        var sp = await serviceProviderStore.FindByEntityIdAsync(data.SamlServiceProviderEntityId, context.RequestAborted);
        if (sp == null)
        {
            logger.ServiceProviderNotFound(data.SamlServiceProviderEntityId);
            return new Saml2FrontChannelResult { Error = "SAML service provider not found" };
        }

        if (!sp.Enabled)
        {
            logger.ServiceProviderDisabled(sp.EntityId);
            return new Saml2FrontChannelResult { Error = "SAML service provider is disabled" };
        }

        if (sp.SingleLogoutServiceUrls.Count == 0)
        {
            logger.ServiceProviderHasNoSingleLogoutServiceUrl(sp.EntityId);
            return new Saml2FrontChannelResult { Error = "SAML service provider has no logout endpoint configured" };
        }

        // Build a minimal ValidatedLogoutRequest to pass to the response generator
        var sloEndpoint = sp.GetSingleLogoutServiceEndpoint(SamlBinding.HttpRedirect);
        if (sloEndpoint == null)
        {
            logger.ServiceProviderHasNoSingleLogoutServiceUrl(sp.EntityId);
            return new Saml2FrontChannelResult { Error = "SAML service provider has no HTTP-Redirect logout endpoint configured" };
        }

        var spBinding = sloEndpoint.Binding.ToUrn();

        var idpEntityId = await issuerNameService.GetCurrentAsync(context.RequestAborted);

        var validatedRequest = new ValidatedLogoutRequest
        {
            LogoutRequest = new SamlLogoutRequest
            {
                Id = data.SamlLogoutRequestId
            },
            Binding = spBinding,
            RelayState = data.SamlRelayState,
            Saml2Sp = sp,
            Saml2IdpEntityId = idpEntityId
        };

        // Determine success/partial based on tracked SP responses.
        // Note: if ExpectedResponses is empty (no other SPs needed notification), All() returns true → Success.
        var logoutSession = await samlLogoutSessionStore.GetByLogoutIdAsync(logoutId, context.RequestAborted);
        var allSucceeded = logoutSession != null &&
            logoutSession.SkippedSpCount == 0 &&
            logoutSession.ExpectedResponses.Values.All(e => e.Response is { Success: true });

        if (logoutSession == null)
        {
            logger.NoLogoutSessionFound(LogLevel.Debug, logoutId);
        }
        else
        {
            var total = logoutSession.ExpectedResponses.Count;
            var received = logoutSession.ExpectedResponses.Values.Count(e => e.Response != null);
            var succeeded = logoutSession.ExpectedResponses.Values.Count(e => e.Response is { Success: true });

            logger.LogoutSessionStatus(LogLevel.Debug, logoutId, received, total, succeeded, logoutSession.SkippedSpCount, allSucceeded ? "Success" : "PartialLogout");
        }

        var response = allSucceeded
            ? await responseGenerator.CreateSuccessResponse(validatedRequest, context.RequestAborted)
            : await responseGenerator.CreatePartialLogoutResponse(validatedRequest, context.RequestAborted);

        if (allSucceeded)
        {
            await events.RaiseAsync(new SamlSloSuccessEvent(sp.EntityId, null, "SP"), context.RequestAborted);
            Telemetry.Metrics.SamlSlo(sp.EntityId);
        }
        else
        {
            await events.RaiseAsync(new SamlSloFailureEvent(sp.EntityId, "Partial logout - not all SPs responded successfully"), context.RequestAborted);
            Telemetry.Metrics.SamlSloFailure(sp.EntityId, "partial_logout");
        }

        if (logoutSession != null)
        {
            await samlLogoutSessionStore.RemoveAsync(logoutId, context.RequestAborted);
        }

        return response;
    }
}
