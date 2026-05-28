// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Saml.Services;

/// <summary>
/// Default implementation of <see cref="ISamlLogoutNotificationService"/> that uses the new
/// <see cref="ISaml2FrontChannelLogoutRequestBuilder"/> to build outbound LogoutRequest messages.
/// </summary>
public sealed class Saml2LogoutNotificationService(
    ISaml2IssuerNameService issuerNameService,
    ISamlServiceProviderStore serviceProviderStore,
    ISaml2FrontChannelLogoutRequestBuilder frontChannelLogoutRequestBuilder,
    ILogger<Saml2LogoutNotificationService> logger) : ISamlLogoutNotificationService
{
    /// <inheritdoc/>
    public async Task<SamlLogoutNotificationResult> GetSamlFrontChannelLogoutsAsync(
        LogoutNotificationContext context,
        Ct ct)
    {
        using var activity = Tracing.ServiceActivitySource.StartActivity("Saml2LogoutNotificationService.GetSamlFrontChannelLogouts");

        var results = new List<SamlLogoutRequestContext>();
        var skippedCount = 0;

        if (context.SamlSessions.Count == 0)
        {
            logger.NoSamlServiceProvidersToNotifyForLogout(LogLevel.Debug);
            return new SamlLogoutNotificationResult(results, 0);
        }

        var issuer = await issuerNameService.GetCurrentAsync(ct);

        foreach (var sessionData in context.SamlSessions)
        {
            // Skip the SP that initiated the logout — it will receive a LogoutResponse instead.
            if (string.Equals(sessionData.EntityId, context.SamlInitiatingServiceProviderEntityId, StringComparison.Ordinal))
            {
                logger.SkippingLogoutNotificationForInitiatingServiceProvider(LogLevel.Debug, sessionData.EntityId);
                continue;
            }

            var sp = await serviceProviderStore.FindByEntityIdAsync(sessionData.EntityId, ct);
            if (sp?.Enabled != true)
            {
                logger.SkippingLogoutUrlGenerationForUnknownOrDisabledServiceProvider(LogLevel.Debug, sessionData.EntityId);
                skippedCount++;
                continue;
            }

            if (sp.SingleLogoutServiceUrls.Count == 0)
            {
                logger.SkippingLogoutUrlGenerationForServiceProviderWithNoSingleLogout(LogLevel.Debug, sessionData.EntityId);
                skippedCount++;
                continue;
            }

            var sloEndpoint = sp.GetSingleLogoutServiceEndpoint(SamlBinding.HttpRedirect);
            if (sloEndpoint == null)
            {
                logger.SkippingLogoutUrlGenerationForUnsupportedBinding(LogLevel.Debug, sessionData.EntityId, sp.SingleLogoutServiceUrls.FirstOrDefault()?.Binding ?? default);
                skippedCount++;
                continue;
            }

            try
            {
                var requestContext = await frontChannelLogoutRequestBuilder.BuildLogoutRequestAsync(
                    sp,
                    sessionData.NameId,
                    sessionData.NameIdFormat,
                    sessionData.SessionIndex,
                    issuer,
                    ct);

                results.Add(requestContext);
            }
#pragma warning disable CA1031 // Do not catch general exception types: one failure should not stop the whole process
            catch (Exception ex)
#pragma warning restore CA1031
            {
                logger.FailedToGenerateLogoutUrlForServiceProvider(ex, sessionData.EntityId);
                skippedCount++;
            }
        }

        if (results.Count > 0)
        {
            logger.GeneratedSamlFrontChannelLogoutUrls(LogLevel.Debug, results.Count);
        }
        else
        {
            logger.NoSamlFrontChannelLogoutUrlsGenerated(LogLevel.Debug);
        }

        return new SamlLogoutNotificationResult(results, skippedCount);
    }
}
