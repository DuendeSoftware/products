// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using System.Security.Claims;
using System.Text;
using Duende.IdentityModel;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Saml.Models;
using Duende.IdentityServer.Saml.ResponseHandling;
using Duende.IdentityServer.Saml.Validation;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Stores;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Duende.IdentityServer.Saml.Services.Default;

/// <summary>
/// Default implementation of <see cref="IIdpInitiatedSsoService"/>.
/// Validates the target SP, generates a signed SAML response, records the SP
/// session for SLO, and returns an <see cref="IdpInitiatedSsoResult"/> containing
/// the HTML auto-POST form.
/// </summary>
public sealed class DefaultIdpInitiatedSsoService(
    ISamlServiceProviderStore serviceProviderStore,
    ISamlResourceResolver resourceResolver,
    IUserSession userSession,
    ISaml2SsoResponseGenerator responseGenerator,
    ISaml2IssuerNameService issuerNameService,
    IOptions<IdentityServerOptions> identityServerOptions,
    ILogger<DefaultIdpInitiatedSsoService> logger)
    : IIdpInitiatedSsoService
{
    private readonly IdentityServerOptions _identityServerOptions = identityServerOptions.Value;
    private readonly SamlOptions _samlOptions = identityServerOptions.Value.Saml;

    /// <inheritdoc/>
    public Task<IdpInitiatedSsoResult> CreateResponseAsync(
        HttpContext httpContext,
        string spEntityId,
        Ct ct) => CreateResponseAsync(httpContext, spEntityId, null, ct);

    /// <inheritdoc/>
    public async Task<IdpInitiatedSsoResult> CreateResponseAsync(
        HttpContext httpContext,
        string spEntityId,
        string? relayState,
        Ct ct)
    {
        using var activity = Tracing.BasicActivitySource.StartActivity("IdpInitiatedSsoService");

        if (string.IsNullOrWhiteSpace(spEntityId))
        {
            logger.IdpInitiatedSsoMissingSpEntityId(LogLevel.Debug);
            return IdpInitiatedSsoResult.Failure("Missing required 'spEntityId' parameter");
        }

        var sp = await serviceProviderStore.FindByEntityIdAsync(spEntityId, ct);
        if (sp == null)
        {
            logger.ServiceProviderNotFound(LogLevel.Debug, spEntityId);
            return IdpInitiatedSsoResult.Failure("Service provider not found", spEntityId);
        }

        if (!sp.Enabled)
        {
            logger.ServiceProviderIsDisabled(LogLevel.Debug, spEntityId);
            return IdpInitiatedSsoResult.Failure("Service provider is disabled", spEntityId);
        }

        if (!sp.AllowIdpInitiated)
        {
            logger.ServiceProviderDoesNotAllowIdpInitiatedSso(LogLevel.Debug, spEntityId);
            return IdpInitiatedSsoResult.Failure("Service provider does not allow IdP-initiated SSO", spEntityId);
        }

        // Normalize empty relay state to null so downstream code has a single path.
        if (string.IsNullOrEmpty(relayState))
        {
            relayState = null;
        }

        if (relayState != null)
        {
            var relayStateByteCount = Encoding.UTF8.GetByteCount(relayState);
            if (relayStateByteCount > _samlOptions.MaxRelayStateLength)
            {
                logger.RelayStateExceedsMaxLength(LogLevel.Debug, _samlOptions.MaxRelayStateLength);
                return IdpInitiatedSsoResult.Failure(
                    $"RelayState exceeds maximum length of {_samlOptions.MaxRelayStateLength} bytes",
                    spEntityId);
            }
        }

        var acsEndpoint = sp.AssertionConsumerServiceUrls.FirstOrDefault(a => a.IsDefault)
            ?? sp.AssertionConsumerServiceUrls.FirstOrDefault();
        if (acsEndpoint == null)
        {
            logger.ServiceProviderHasNoAssertionConsumerServiceUrls(LogLevel.Debug, spEntityId);
            return IdpInitiatedSsoResult.Failure(
                "Service provider has no assertion consumer service URLs configured", spEntityId);
        }

        if (!Uri.TryCreate(acsEndpoint.Location, UriKind.Absolute, out _))
        {
            logger.ServiceProviderHasInvalidAcsUrl(spEntityId, acsEndpoint.Location);
            return IdpInitiatedSsoResult.Failure(
                "Service provider has an invalid assertion consumer service URL configured", spEntityId);
        }

        var user = await userSession.GetUserAsync(ct);
        if (user?.Identity?.IsAuthenticated != true)
        {
            logger.UserIsNotAuthenticated(LogLevel.Debug);
            return IdpInitiatedSsoResult.Failure("User is not authenticated", spEntityId);
        }

        var sid = await userSession.GetSessionIdAsync(ct);
        var idpEntityId = await issuerNameService.GetCurrentAsync(ct);

        var existingSessions = await userSession.GetSamlSessionListAsync(ct);
        var existingSession = existingSessions.FirstOrDefault(s => s.EntityId == sp.EntityId);
        var sessionIndex = existingSession?.SessionIndex ?? Guid.NewGuid().ToString("N");

        var resourceResult = await resourceResolver.ResolveRequestedClaimTypesAsync(sp, ct);
        if (!resourceResult.Succeeded)
        {
            logger.ResourceResolutionFailed(spEntityId, resourceResult.Error);
            return IdpInitiatedSsoResult.Failure(resourceResult.Error, spEntityId);
        }

        var validatedAuthnRequest = new ValidatedAuthnRequest
        {
            IdentityServerOptions = _identityServerOptions,
            Binding = SamlConstants.Bindings.HttpPost,
            RelayState = relayState,
            Saml2IdpEntityId = idpEntityId,
            Saml2Sp = sp,
            Subject = user,
            SessionId = sid,
            AssertionConsumerService = acsEndpoint,
            IsIdpInitiated = true,
            SessionIndex = sessionIndex,
            RequestedClaimTypes = resourceResult.ClaimTypes
        };

        var response = await responseGenerator.CreateResponse(validatedAuthnRequest, ct);

        if (response.Message == null)
        {
            logger.ResponseGeneratorReturnedNoMessage(spEntityId);
            return IdpInitiatedSsoResult.Failure("Failed to generate SAML response", spEntityId);
        }

        await RecordSpSessionAsync(user, sp, sessionIndex, ct);

        return IdpInitiatedSsoResult.Success(new SamlAutoPostResult(response), spEntityId);
    }

    private async Task RecordSpSessionAsync(ClaimsPrincipal user, SamlServiceProvider sp, string sessionIndex, Ct ct)
    {
        var sessionData = new SamlSpSessionData
        {
            EntityId = sp.EntityId,
            SessionIndex = sessionIndex,
            NameId = user.FindFirstValue(JwtClaimTypes.Subject) ?? string.Empty,
            NameIdFormat = sp.DefaultNameIdFormat ?? SamlConstants.NameIdentifierFormats.Unspecified
        };
        await userSession.AddSamlSessionAsync(sessionData, ct);
    }
}
