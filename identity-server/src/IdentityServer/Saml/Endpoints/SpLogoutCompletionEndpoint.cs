// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System.Net;
using Duende.IdentityServer.Endpoints.Results;
using Duende.IdentityServer.Hosting;
using Duende.IdentityServer.Hosting.FederatedSignOut;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Saml.ResponseHandling;
using Duende.IdentityServer.Saml.Services;
using Duende.IdentityServer.Saml.Validation;
using Duende.IdentityServer.Stores;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using SamlLogoutRequest = Duende.IdentityServer.Saml.Samlp.LogoutRequest;

namespace Duende.IdentityServer.Saml.Endpoints;

/// <summary>
/// Endpoint that generates a SAML LogoutResponse and sends it back to the upstream IdP.
/// For HTTP-Redirect binding, returns a 302 redirect.
/// For HTTP-POST binding, returns an auto-submit HTML form.
/// Called by the browser after front-channel logout iframes have completed.
/// </summary>
internal sealed class SpLogoutCompletionEndpoint(
    IMessageStore<SamlSpLogoutMessage> logoutMessageStore,
    ISaml2SloResponseGenerator responseGenerator,
    ISaml2IssuerNameService issuerNameService,
    ISamlLogoutSessionStore samlLogoutSessionStore,
    TimeProvider timeProvider,
    ILogger<SpLogoutCompletionEndpoint> logger) : IEndpointHandler
{
    private static readonly TimeSpan MaxLogoutAge = TimeSpan.FromMinutes(5);

    public async Task<IEndpointResult?> ProcessAsync(HttpContext context)
    {
        using var activity = Tracing.BasicActivitySource.StartActivity(IdentityServerConstants.EndpointNames.SamlSpLogoutCompletion + "Endpoint");

        if (!HttpMethods.IsGet(context.Request.Method))
        {
            return new StatusCodeResult(HttpStatusCode.MethodNotAllowed);
        }

        var logoutId = context.Request.Query["logoutId"].ToString();
        if (string.IsNullOrWhiteSpace(logoutId))
        {
            logger.LogWarning("SP logout completion request missing logoutId parameter");
            return new StatusCodeResult(HttpStatusCode.BadRequest);
        }

        var logoutMessage = await logoutMessageStore.ReadAsync(logoutId, context.RequestAborted);
        if (logoutMessage?.Data == null)
        {
            logger.LogWarning("SP logout completion: no message found for logoutId {LogoutId}", logoutId);
            return new StatusCodeResult(HttpStatusCode.BadRequest);
        }

        // TTL validation beyond store expiry
        var messageAge = timeProvider.GetUtcNow() - new DateTimeOffset(logoutMessage.Created, TimeSpan.Zero);
        if (messageAge > MaxLogoutAge)
        {
            logger.LogWarning("SP logout completion: message expired for logoutId {LogoutId}", logoutId);
            return new StatusCodeResult(HttpStatusCode.BadRequest);
        }

        var data = logoutMessage.Data;

        if (string.IsNullOrWhiteSpace(data.IdpEntityId) ||
            string.IsNullOrWhiteSpace(data.LogoutRequestId) ||
            string.IsNullOrWhiteSpace(data.ResponseBinding) ||
            string.IsNullOrWhiteSpace(data.ResponseDestination))
        {
            logger.LogWarning("SP logout completion: message missing required SAML fields for logoutId {LogoutId}", logoutId);
            return new StatusCodeResult(HttpStatusCode.BadRequest);
        }

        // Determine success/partial based on tracked SP responses.
        // If no logout session exists (no downstream SAML SPs), treat as success —
        // downstream OIDC clients were already notified via front-channel iframes.
        var logoutSession = await samlLogoutSessionStore.GetByLogoutIdAsync(logoutId, context.RequestAborted);
        var allSucceeded = logoutSession == null ||
            (logoutSession.SkippedSpCount == 0 &&
            logoutSession.ExpectedResponses.Values.All(e => e.Response is { Success: true }));

        // Build a ValidatedLogoutRequest for the response generator.
        // This SamlServiceProvider object represents the upstream IdP as the response destination,
        // not a downstream SP. The response generator uses its EntityId and SingleLogoutServiceUrl
        // to determine where to send the LogoutResponse.
        var upstreamIdpDescriptor = new SamlServiceProvider
        {
            EntityId = data.IdpEntityId,
            SingleLogoutServiceUrls = [new SamlEndpointType
            {
                Location = data.ResponseDestination,
                Binding = data.ResponseBinding switch
                {
                    SamlConstants.Bindings.HttpPost => SamlBinding.HttpPost,
                    SamlConstants.Bindings.HttpRedirect => SamlBinding.HttpRedirect,
                    _ => throw new InvalidOperationException($"Unsupported SAML logout response binding: {data.ResponseBinding}")
                }
            }]
        };

        var validatedRequest = new ValidatedLogoutRequest
        {
            LogoutRequest = new SamlLogoutRequest
            {
                Id = data.LogoutRequestId
            },
            Binding = data.ResponseBinding,
            RelayState = data.RelayState,
            Saml2Sp = upstreamIdpDescriptor,
            Saml2IdpEntityId = await issuerNameService.GetCurrentAsync(context.RequestAborted)
        };

        var response = allSucceeded
            ? await responseGenerator.CreateSuccessResponse(validatedRequest, context.RequestAborted)
            : await responseGenerator.CreatePartialLogoutResponse(validatedRequest, context.RequestAborted);

        if (logoutSession != null)
        {
            await samlLogoutSessionStore.RemoveAsync(logoutId, context.RequestAborted);
        }

        if (response.Message == null)
        {
            logger.LogError("SP logout completion: response generator returned null message for logoutId {LogoutId}", logoutId);
            return new StatusCodeResult(HttpStatusCode.InternalServerError);
        }

        // Return the standard Saml2FrontChannelResult — the existing
        // Saml2FrontChannelResultHttpWriter resolves the correct binding
        // (HTTP-Redirect or HTTP-POST) and handles encoding/compression.
        return response;
    }
}
