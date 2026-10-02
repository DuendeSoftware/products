// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.EntityFramework;

internal static partial class Log
{
    [LoggerMessage(EventId = 0, Level = LogLevel.Trace, Message = "Querying for expired grants to remove")]
    internal static partial void QueryingForExpiredGrantsToRemove(ILogger logger);

    [LoggerMessage(EventId = 0, Level = LogLevel.Error, Message = "Exception removing expired grants: {Exception}")]
    internal static partial void ExceptionRemovingExpiredGrantsValue(ILogger logger, object exception);

    [LoggerMessage(EventId = 0, Level = LogLevel.Information, Message = "Removed {GrantCount} expired grants")]
    internal static partial void RemovedValueExpiredGrants(ILogger logger, int grantCount);

    [LoggerMessage(EventId = 0, Level = LogLevel.Information, Message = "Removing {GrantCount} expired grants")]
    internal static partial void RemovingValueExpiredGrants(ILogger logger, int grantCount);

    [LoggerMessage(EventId = 0, Level = LogLevel.Warning, Message = "Tried to remove {GrantCount} expired grants, but only {DeleteCount} was deleted. This indicates that another process has already removed the items. Duplicate notifications may be sent to the registered IOperationalStoreNotification.")]
    internal static partial void TriedToRemoveValueExpiredGrantsButOnly(ILogger logger, int grantCount, int deleteCount);

    [LoggerMessage(EventId = 0, Level = LogLevel.Information, Message = "Removed {GrantCount} consumed grants")]
    internal static partial void RemovedValueConsumedGrants(ILogger logger, int grantCount);

    [LoggerMessage(EventId = 0, Level = LogLevel.Information, Message = "Removing {GrantCount} consumed grants")]
    internal static partial void RemovingValueConsumedGrants(ILogger logger, int grantCount);

    [LoggerMessage(EventId = 0, Level = LogLevel.Warning, Message = "Tried to remove {GrantCount} consumed grants, but only {DeleteCount} was deleted. This indicates that another process has already removed the items. Duplicate notifications may be sent to the registered IOperationalStoreNotification.")]
    internal static partial void TriedToRemoveValueConsumedGrantsButOnly(ILogger logger, int grantCount, int deleteCount);

    [LoggerMessage(EventId = 0, Level = LogLevel.Information, Message = "Removed {DeviceCodeCount} device flow codes")]
    internal static partial void RemovedValueDeviceFlowCodes(ILogger logger, int deviceCodeCount);

    [LoggerMessage(EventId = 0, Level = LogLevel.Information, Message = "Removing {DeviceCodeCount} device flow codes")]
    internal static partial void RemovingValueDeviceFlowCodes(ILogger logger, int deviceCodeCount);

    [LoggerMessage(EventId = 0, Level = LogLevel.Warning, Message = "Tried to remove {GrantCount} expired device codes, but only {DeleteCount} was deleted. This indicates that another process has already removed the items. Duplicate notifications may be sent to the registered IOperationalStoreNotification.")]
    internal static partial void TriedToRemoveValueExpiredDeviceCodesBut(ILogger logger, int grantCount, int deleteCount);

    [LoggerMessage(EventId = 0, Level = LogLevel.Information, Message = "Removing {ParCount} stale pushed authorization requests")]
    internal static partial void RemovingValueStalePushedAuthorizationRequests(ILogger logger, int parCount);

    [LoggerMessage(EventId = 0, Level = LogLevel.Warning, Message = "Tried to remove {ParCount} stale pushed authorization requests, but only {DeleteCount} items were deleted. This indicates that another process has already removed the items.")]
    internal static partial void TriedToRemoveValueStalePushedAuthorizationRequests(ILogger logger, int parCount, int deleteCount);

    [LoggerMessage(EventId = 0, Level = LogLevel.Information, Message = "Removed {Count} stale SAML signin states")]
    internal static partial void RemovedValueStaleSamlSigninStates(ILogger logger, int count);

    [LoggerMessage(EventId = 0, Level = LogLevel.Information, Message = "Removing {Count} stale SAML signin states")]
    internal static partial void RemovingValueStaleSamlSigninStates(ILogger logger, int count);

    [LoggerMessage(EventId = 0, Level = LogLevel.Warning, Message = "Tried to remove {Count} stale SAML signin states, but only {DeleteCount} was deleted. This indicates that another process has already removed the items. Duplicate notifications may be sent to the registered IOperationalStoreNotification.")]
    internal static partial void TriedToRemoveValueStaleSamlSigninStates(ILogger logger, int count, int deleteCount);

    [LoggerMessage(EventId = 0, Level = LogLevel.Information, Message = "Removed {Count} expired SAML logout sessions")]
    internal static partial void RemovedValueExpiredSamlLogoutSessions(ILogger logger, int count);

    [LoggerMessage(EventId = 0, Level = LogLevel.Information, Message = "Removing {Count} expired SAML logout sessions")]
    internal static partial void RemovingValueExpiredSamlLogoutSessions(ILogger logger, int count);

    [LoggerMessage(EventId = 0, Level = LogLevel.Warning, Message = "Tried to remove {Count} expired SAML logout sessions, but only {DeleteCount} was deleted. This indicates that another process has already removed the items. Duplicate notifications may be sent to the registered IOperationalStoreNotification.")]
    internal static partial void TriedToRemoveValueExpiredSamlLogoutSessions(ILogger logger, int count, int deleteCount);

}
