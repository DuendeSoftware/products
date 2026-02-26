// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Internal.Saml.Infrastructure;
using Duende.IdentityServer.Internal.Saml.SingleLogout.Models;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Internal.Saml.SingleLogout;

/// <summary>
/// Processes SAML Single Logout callback requests after user logout completes.
/// </summary>
internal class SamlLogoutCallbackProcessor(
    IMessageStore<LogoutMessage> logoutMessageStore,
    ISamlServiceProviderStore serviceProviderStore,
    LogoutResponseBuilder logoutResponseBuilder,
    ILogger<SamlLogoutCallbackProcessor> logger)
{
    internal async Task<Result<LogoutResponse, SamlLogoutCallbackError>> ProcessAsync(string logoutId, Ct ct = default)
    {
        var logoutMessage = await logoutMessageStore.ReadAsync(logoutId, ct);
        if (logoutMessage?.Data == null)
        {
            logger.NoLogoutMessageFound(LogLevel.Warning, logoutId);
            return new SamlLogoutCallbackError("No logout message found");
        }

        var data = logoutMessage.Data;
        if (data.SamlServiceProviderEntityId == null)
        {
            logger.LogoutMessageMissingSamlEntityId(LogLevel.Warning);
            return new SamlLogoutCallbackError("Logout message does not contain SAML SP entity ID");
        }

        logger.BuildingLogoutResponseForSp(LogLevel.Debug, data.SamlServiceProviderEntityId);

        var sp = await serviceProviderStore.FindByEntityIdAsync(data.SamlServiceProviderEntityId, ct);
        if (sp == null)
        {
            logger.ServiceProviderNotFound(LogLevel.Error, data.SamlServiceProviderEntityId);
            return new SamlLogoutCallbackError($"Service Provider not found: {data.SamlServiceProviderEntityId}");
        }

        if (!sp.Enabled)
        {
            logger.ServiceProviderDisabled(LogLevel.Error, sp.EntityId);
            return new SamlLogoutCallbackError($"Service Provider is disabled: {sp.EntityId}");
        }

        if (sp.SingleLogoutServiceUrl == null)
        {
            logger.SamlLogoutNoSingleLogoutServiceUrl(LogLevel.Error, sp.EntityId);
            return new SamlLogoutCallbackError($"Service Provider has no SingleLogoutServiceUrl configured: {sp.EntityId}");
        }

        if (string.IsNullOrWhiteSpace(data.SamlLogoutRequestId))
        {
            logger.LogoutMessageMissingRequestId(LogLevel.Error);
            return new SamlLogoutCallbackError("Logout message does not contain SAML logout request ID");
        }

        var response = await logoutResponseBuilder.BuildSuccessResponseAsync(
            data.SamlLogoutRequestId,
            sp,
            data.SamlRelayState,
            ct);

        logger.SuccessfullyBuiltLogoutResponse(LogLevel.Information, data.SamlServiceProviderEntityId, data.SamlLogoutRequestId);

        return response;
    }
}

/// <summary>
/// Represents an error during SAML logout callback processing.
/// </summary>
internal record SamlLogoutCallbackError(string Message);
