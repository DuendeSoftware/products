// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Saml.Models;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Stores;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Duende.IdentityServer.Saml.Validation;

/// <summary>
/// LogoutRequest validator
/// </summary>
public class LogoutRequestValidator(
    ISamlServiceProviderStore serviceProviderStore,
    IUserSession userSession,
    TimeProvider timeProvider,
    IOptions<IdentityServerOptions> identityServerOptions,
    IServerUrls serverUrls,
    ILogger<LogoutRequestValidator> logger)
    : ILogoutRequestValidator
{
    // Replay protection is intentionally omitted for LogoutRequests. The safe failure
    // mode for logout is to complete it — if a browser retries due to a connection issue,
    // the logout should still succeed. Re-processing a signed, time-bounded LogoutRequest
    // only results in re-logout of an already-logged-out session (no privilege gain).

    /// <inheritdoc />
    public async Task<LogoutRequestValidationResult> ValidateAsync(ValidatedLogoutRequest request, Ct ct)
    {
        var spResult = await ValidateSpAsync(request, ct);
        if (spResult.IsError)
        {
            return spResult;
        }

        var signatureResult = ValidateSignatureTrust(request);
        if (signatureResult.IsError)
        {
            return signatureResult;
        }

        var versionResult = ValidateVersion(request);
        if (versionResult.IsError)
        {
            return versionResult;
        }

        var destinationResult = ValidateDestination(request);
        if (destinationResult.IsError)
        {
            return destinationResult;
        }

        var notOnOrAfterResult = ValidateNotOnOrAfter(request);
        if (notOnOrAfterResult.IsError)
        {
            return notOnOrAfterResult;
        }

        var sessionResult = await ValidateSessionAsync(request, ct);

        return sessionResult;
    }

    /// <summary>
    /// Validates the SP from the Issuer entity ID.
    /// </summary>
    protected virtual async Task<LogoutRequestValidationResult> ValidateSpAsync(ValidatedLogoutRequest request, Ct ct)
    {
        if (request.LogoutRequest.Issuer == null)
        {
            return LogoutRequestValidationResult.InValid(request, SamlStatusCodes.Requester, "Missing SP EntityID in LogoutRequest");
        }

        var spEntityId = request.LogoutRequest.Issuer.Value;
        var serviceProvider = await serviceProviderStore.FindByEntityIdAsync(spEntityId, ct);

        if (serviceProvider is not { Enabled: true })
        {
            return LogoutRequestValidationResult.InValid(request, SamlStatusCodes.Requester, "Invalid SP EntityId");
        }

        if (serviceProvider.SingleLogoutServiceUrls.Count == 0)
        {
            return LogoutRequestValidationResult.InValid(request, SamlStatusCodes.Requester, "SP does not have any SingleLogoutServiceUrls configured");
        }

        request.Saml2Sp = serviceProvider;

        return LogoutRequestValidationResult.Valid(request);
    }

    /// <summary>
    /// Validates that the LogoutRequest is signed (required per SAML spec).
    /// </summary>
    protected virtual LogoutRequestValidationResult ValidateSignatureTrust(ValidatedLogoutRequest request)
    {
        if (!request.LogoutRequest.HasTrustedSignature)
        {
            logger.LogoutRequestSignatureTrustCheckFailed(LogLevel.Warning, request.Saml2Sp?.EntityId, request.LogoutRequest.TrustLevel);

            return LogoutRequestValidationResult.InValid(
                request,
                SamlStatusCodes.Requester,
                "The LogoutRequest signature is missing or not trusted");
        }

        return LogoutRequestValidationResult.Valid(request);
    }

    /// <summary>
    /// Validates that the LogoutRequest uses SAML version 2.0.
    /// </summary>
    protected virtual LogoutRequestValidationResult ValidateVersion(ValidatedLogoutRequest request)
    {
        if (request.LogoutRequest.Version != SamlVersions.V2)
        {
            return LogoutRequestValidationResult.InValid(
                request,
                SamlStatusCodes.VersionMismatch,
                "Only Version 2.0 is supported");
        }

        return LogoutRequestValidationResult.Valid(request);
    }

    /// <summary>
    /// Validates that the Destination matches the SLO endpoint URL.
    /// Signed requests must include a Destination per SAML Bindings 2.0.
    /// </summary>
    protected virtual LogoutRequestValidationResult ValidateDestination(ValidatedLogoutRequest request)
    {
        var destination = request.LogoutRequest.Destination;

        if (string.IsNullOrEmpty(destination))
        {
            if (request.LogoutRequest.HasTrustedSignature)
            {
                return LogoutRequestValidationResult.InValid(
                    request,
                    SamlStatusCodes.Requester,
                    "Signed LogoutRequests must include a Destination");
            }

            return LogoutRequestValidationResult.Valid(request);
        }

        var expectedDestination = serverUrls.BaseUrl + identityServerOptions.Value.Saml.Endpoints.SingleLogoutServicePath;

        if (!destination.Equals(expectedDestination, StringComparison.OrdinalIgnoreCase))
        {
            logger.LogoutRequestDestinationMismatch(LogLevel.Warning, expectedDestination, destination);

            return LogoutRequestValidationResult.InValid(
                request,
                SamlStatusCodes.Requester,
                "Invalid destination");
        }

        return LogoutRequestValidationResult.Valid(request);
    }

    /// <summary>
    /// Validates that the NotOnOrAfter has not expired.
    /// </summary>
    protected virtual LogoutRequestValidationResult ValidateNotOnOrAfter(ValidatedLogoutRequest request)
    {
        if (!request.LogoutRequest.NotOnOrAfter.HasValue)
        {
            return LogoutRequestValidationResult.Valid(request);
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var clockSkew = request.Saml2Sp?.ClockSkew ?? identityServerOptions.Value.Saml.DefaultClockSkew;
        var notOnOrAfter = (DateTime)request.LogoutRequest.NotOnOrAfter.Value;

        if (now > notOnOrAfter.Add(clockSkew))
        {
            return LogoutRequestValidationResult.InValid(
                request,
                SamlStatusCodes.Requester,
                "LogoutRequest has expired (NotOnOrAfter)");
        }

        return LogoutRequestValidationResult.Valid(request);
    }

    /// <summary>
    /// Validates that the LogoutRequest's NameID and SessionIndex match an active
    /// SAML session for the requesting SP. Per SAML 2.0 Core §3.7.1, the NameID
    /// identifies the principal and the SessionIndex identifies the specific session
    /// to terminate.
    /// </summary>
    protected virtual async Task<LogoutRequestValidationResult> ValidateSessionAsync(ValidatedLogoutRequest request, Ct ct)
    {
        var logoutRequest = request.LogoutRequest;
        var spEntityId = request.Saml2Sp?.EntityId;

        // If no user is authenticated, skip session validation — the endpoint will
        // return success immediately (nothing to log out).
        if (request.Subject?.Identity?.IsAuthenticated != true)
        {
            return LogoutRequestValidationResult.Valid(request);
        }

        // NameID is required per SAML 2.0 Core §3.7.1
        if (logoutRequest.NameId == null || string.IsNullOrEmpty(logoutRequest.NameId.Value))
        {
            return LogoutRequestValidationResult.InValid(
                request,
                SamlStatusCodes.Requester,
                "LogoutRequest must contain a NameID");
        }

        var sessions = await userSession.GetSamlSessionListAsync(ct);
        var matchingSessions = sessions
            .Where(s => s.EntityId == spEntityId)
            .ToList();

        if (matchingSessions.Count == 0)
        {
            logger.LogoutRequestNoActiveSessionFound(LogLevel.Information, spEntityId);

            return new LogoutRequestValidationResult
            {
                ValidatedRequest = request,
                IsError = false,
                SessionFound = false
            };
        }

        // Validate NameID matches
        var nameIdMatch = matchingSessions.Any(s =>
            string.Equals(s.NameId, logoutRequest.NameId.Value, StringComparison.Ordinal));

        if (!nameIdMatch)
        {
            logger.LogoutRequestNameIdMismatch(LogLevel.Warning, logoutRequest.NameId.Value, spEntityId);

            return LogoutRequestValidationResult.InValid(
                request,
                SamlStatusCodes.Requester,
                "NameID does not match any active session");
        }

        // If SessionIndex is provided, validate it matches too
        if (!string.IsNullOrEmpty(logoutRequest.SessionIndex))
        {
            var sessionIndexMatch = matchingSessions.Any(s =>
                string.Equals(s.SessionIndex, logoutRequest.SessionIndex, StringComparison.Ordinal) &&
                string.Equals(s.NameId, logoutRequest.NameId.Value, StringComparison.Ordinal));

            if (!sessionIndexMatch)
            {
                logger.LogoutRequestSessionIndexMismatch(LogLevel.Information, logoutRequest.SessionIndex, spEntityId);

                return new LogoutRequestValidationResult
                {
                    ValidatedRequest = request,
                    IsError = false,
                    SessionFound = false
                };
            }
        }

        return LogoutRequestValidationResult.Valid(request);
    }
}
