// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Models;
using Duende.IdentityServer.Saml;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Stores;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Internal.Saml.SingleLogout;

internal class SamlLogoutNotificationService(
    IIssuerNameService issuerNameService,
    ISamlServiceProviderStore serviceProviderStore,
    SamlFrontChannelLogoutRequestBuilder frontChannelLogoutRequestBuilder,
    ILogger<SamlLogoutNotificationService> logger) : ISamlLogoutNotificationService
{
    public async Task<IEnumerable<ISamlFrontChannelLogout>> GetSamlFrontChannelLogoutsAsync(LogoutNotificationContext context)
    {
        using var activity = Tracing.ServiceActivitySource.StartActivity("LogoutNotificationService.GetSamlFrontChannelLogoutUrls");

        var logoutUrls = new List<ISamlFrontChannelLogout>();

        if (context.SamlSessions?.Any() == true)
        {
            logger.NoSamlServiceProvidersToNotifyForLogout(LogLevel.Debug);
            return logoutUrls;
        }

        var issuer = await issuerNameService.GetCurrentAsync();

        foreach (var sessionData in context.SamlSessions ?? [])
        {
            var sp = await serviceProviderStore.FindByEntityIdAsync(sessionData.EntityId);
            if (sp?.Enabled != true)
            {
                logger.SkippingLogoutUrlGenerationForUnknownOrDisabledServiceProvider(LogLevel.Debug, sessionData.EntityId);
                continue;
            }

            if (sp.SingleLogoutServiceUrl == null)
            {
                logger.SkippingLogoutUrlGenerationForServiceProviderWithNoSingleLogout(LogLevel.Debug, sessionData.EntityId);
                continue;
            }

            try
            {
                var logoutUrl = await frontChannelLogoutRequestBuilder.BuildLogoutRequestAsync(
                    sp,
                    sessionData.NameId,
                    sessionData.NameIdFormat,
                    sessionData.SessionIndex,
                    issuer);

                logoutUrls.Add(logoutUrl);
            }
#pragma warning disable CA1031 // Do not catch general exception types: one failure should not stop the whole process
            catch (Exception ex)
#pragma warning restore CA1031
            {
                logger.FailedToGenerateLogoutUrlForServiceProvider(ex, sessionData.EntityId);
            }
        }

        if (logoutUrls.Count > 0)
        {
            logger.GeneratedSamlFrontChannelLogoutUrls(LogLevel.Debug, logoutUrls.Count);
        }
        else
        {
            logger.NoSamlFrontChannelLogoutUrlsGenerated(LogLevel.Debug);
        }

        return logoutUrls;
    }
}
