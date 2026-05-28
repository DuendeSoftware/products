// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Events;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Hosting;
using Duende.IdentityServer.Saml.Bindings;
using Duende.IdentityServer.Saml.Endpoints.Results;
using Duende.IdentityServer.Saml.Models;
using Duende.IdentityServer.Saml.ResponseHandling;
using Duende.IdentityServer.Saml.Samlp;
using Duende.IdentityServer.Saml.Serialization;
using Duende.IdentityServer.Saml.Services;
using Duende.IdentityServer.Saml.Validation;
using Duende.IdentityServer.Saml.Xml;
using Duende.IdentityServer.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Saml.Endpoints;

internal sealed class SingleSignOnServiceEndpoint(
    IEnumerable<IFrontChannelBinding> frontChannelBindings,
    ServiceProviderEntityResolver serviceProviderEntityResolver,
    ISamlXmlReader samlXmlReader,
    IUserSession userSession,
    IdentityServerOptions identityServerOptions,
    IAuthnRequestValidator authnRequestValidator,
    ISaml2IssuerNameService saml2IssuerNameService,
    ISaml2SsoInteractionResponseGenerator interactionResponseGenerator,
    ISaml2SsoResponseGenerator responseGenerator,
    IEventService events,
    ILogger<SingleSignOnServiceEndpoint> logger) : IEndpointHandler
{
    public async Task<IEndpointResult?> ProcessAsync(HttpContext context)
    {
        using var activity = Tracing.BasicActivitySource.StartActivity(IdentityServerConstants.EndpointNames.SamlSingleSignOnService + "Endpoint");

        var binding = frontChannelBindings.FirstOrDefault(binding => binding.CanUnBind(context.Request));
        if (binding == null)
        {
            return new Saml2FrontChannelResult
            {
                Error = "No front channel bindings found to satisfy request"
            };
        }

        InboundSaml2Message requestMessage;
        try
        {
            requestMessage = await binding.UnBindAsync(context.Request, serviceProviderEntityResolver.ResolveAsync);
        }
        catch (FormatException)
        {
            return new Saml2FrontChannelResult
            {
                Error = "Invalid base64 encoding in SAML signin request"
            };
        }

        AuthnRequest authnRequest;
        try
        {
            var traverser = new XmlTraverser(requestMessage.Xml, requestMessage.TrustLevel);
            authnRequest = await samlXmlReader.ReadAuthnRequestAsync(
                traverser, identityServerOptions.Saml.AuthnRequestErrorInspector, context.RequestAborted);
        }
        catch (SamlXmlException e)
        {
            logger.SamlParsingError(e);
            return new Saml2FrontChannelResult
            {
                Error = "The SAML request could not be processed"
            };
        }

        var user = await userSession.GetUserAsync(context.RequestAborted);
        var sid = await userSession.GetSessionIdAsync(context.RequestAborted);

        var validatedAuthnRequest = new ValidatedAuthnRequest
        {
            IdentityServerOptions = identityServerOptions,
            AuthnRequest = authnRequest,
            Binding = binding.Identifier,
            Saml2Message = requestMessage,
            RelayState = requestMessage.RelayState,
            Subject = user,
            SessionId = sid,
            Saml2IdpEntityId = await saml2IssuerNameService.GetCurrentAsync(context.RequestAborted),
            RequestId = authnRequest?.Id,
            NameIdPolicyFormat = authnRequest?.NameIdPolicy?.Format
        };

        var requestValidationResult =
            await authnRequestValidator.ValidateAsync(validatedAuthnRequest, context.RequestAborted);

        if (requestValidationResult.IsError)
        {
            await events.RaiseAsync(new SamlAuthnRequestValidationFailureEvent(
                requestValidationResult.ValidatedRequest.AuthnRequest?.Issuer?.Value,
                requestValidationResult.ErrorDescription ?? "Unknown validation error",
                binding.Identifier), context.RequestAborted);

            // Use the resolved SP entity ID only if validation confirmed the SP exists in
            // configuration. The raw Issuer from the request is attacker-controlled and could
            // cause unbounded metric cardinality if used directly as a tag value.
            var resolvedSpEntityId = requestValidationResult.ValidatedRequest.Saml2Sp?.EntityId;
            Telemetry.Metrics.SamlSsoFailure(
                resolvedSpEntityId ?? "invalid",
                requestValidationResult.Error ?? "unknown");

            return new Saml2FrontChannelResult()
            {
                Error = requestValidationResult.ErrorDescription,
                SpEntityId = requestValidationResult.ValidatedRequest.AuthnRequest?.Issuer?.Value,
            };
        }

        var interactionResponse = await interactionResponseGenerator.ProcessInteractionAsync(validatedAuthnRequest, context.RequestAborted);

        var interactionResult = await CreateInteractionResultAsync(validatedAuthnRequest, interactionResponse, context.RequestAborted);

        if (interactionResult != null)
        {
            return interactionResult;
        }

        // When responding directly (no interaction needed), generate a session index
        // and track the SAML session, mirroring what the callback endpoint does.
        var ct = context.RequestAborted;
        var existingSessions = await userSession.GetSamlSessionListAsync(ct);
        var existingSession = existingSessions.FirstOrDefault(s => s.EntityId == validatedAuthnRequest.Saml2Sp!.EntityId);
        var sessionIndex = existingSession?.SessionIndex ?? Guid.NewGuid().ToString("N");

        validatedAuthnRequest.SessionIndex = sessionIndex;

        var response = await responseGenerator.CreateResponse(validatedAuthnRequest, ct);

        if (response.GeneratedNameId != null)
        {
            var sessionData = new SamlSpSessionData
            {
                EntityId = validatedAuthnRequest.Saml2Sp!.EntityId,
                SessionIndex = sessionIndex,
                NameId = response.GeneratedNameId.Value,
                NameIdFormat = response.GeneratedNameId.Format
            };
            await userSession.AddSamlSessionAsync(sessionData, ct);
        }

        await events.RaiseAsync(new SamlSsoSuccessEvent(
            validatedAuthnRequest.Saml2Sp!.EntityId,
            user?.GetSubjectId(),
            sessionIndex,
            validatedAuthnRequest.AssertionConsumerService!.Binding.ToUrn(),
            response.GeneratedNameId?.Format), ct);

        Telemetry.Metrics.SamlSso(
            validatedAuthnRequest.Saml2Sp!.EntityId,
            validatedAuthnRequest.AssertionConsumerService!.Binding.ToUrn());

        return response;
    }

    private async Task<IEndpointResult?> CreateInteractionResultAsync(ValidatedAuthnRequest validatedAuthnRequest, Saml2InteractionResponse interactionResponse, Ct ct)
    {
        if (interactionResponse.IsLogin)
        {
            return new Saml2LoginPageResult(
                validatedAuthnRequest,
                identityServerOptions.UserInteraction.LoginUrl,
                identityServerOptions.UserInteraction.LoginReturnUrlParameter);
        }

        if (interactionResponse.IsError)
        {
            await events.RaiseAsync(new SamlSsoFailureEvent(
                validatedAuthnRequest.Saml2Sp!.EntityId,
                interactionResponse.Message ?? "Interaction error",
                "SingleSignOnService"), ct);
            Telemetry.Metrics.SamlSsoFailure(
                validatedAuthnRequest.Saml2Sp!.EntityId,
                interactionResponse.SubStatusCode ?? "interaction_error");
            return await responseGenerator.CreateErrorResponse(validatedAuthnRequest, interactionResponse, ct);
        }

        return null;
    }
}
