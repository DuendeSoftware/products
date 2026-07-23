// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Stores.Storage.PersistedGrants;

internal static partial class Log
{
    [LoggerMessage(
        EventName = nameof(UpdatingPersistedGrant),
        Message = $"{{{LogParameters.PersistedGrantKey}}} found in database, updating")]
    internal static partial void UpdatingPersistedGrant(this ILogger logger, LogLevel logLevel, string persistedGrantKey);

    [LoggerMessage(
        EventName = nameof(CreatingPersistedGrant),
        Message = $"{{{LogParameters.PersistedGrantKey}}} not found in database, creating")]
    internal static partial void CreatingPersistedGrant(this ILogger logger, LogLevel logLevel, string persistedGrantKey);

    [LoggerMessage(
        EventName = nameof(PersistedGrantFound),
        Message = $"{{{LogParameters.PersistedGrantKey}}} found in database: {{{LogParameters.PersistedGrantKeyFound}}}")]
    internal static partial void PersistedGrantFound(this ILogger logger, LogLevel logLevel, string persistedGrantKey, bool persistedGrantKeyFound);

    [LoggerMessage(
        EventName = nameof(PersistedGrantsFound),
        Message = $"{{{LogParameters.PersistedGrantCount}}} persisted grants found for {{@{LogParameters.Filter}}}")]
    internal static partial void PersistedGrantsFound(this ILogger logger, LogLevel logLevel, int persistedGrantCount, PersistedGrantFilter filter);

    [LoggerMessage(
        EventName = nameof(RemovingPersistedGrant),
        Message = $"Removing {{{LogParameters.PersistedGrantKey}}} persisted grant from database")]
    internal static partial void RemovingPersistedGrant(this ILogger logger, LogLevel logLevel, string persistedGrantKey);

    [LoggerMessage(
        EventName = nameof(RemovingPersistedGrants),
        Message = $"Removing persisted grants from database for {{@{LogParameters.Filter}}}")]
    internal static partial void RemovingPersistedGrants(this ILogger logger, LogLevel logLevel, PersistedGrantFilter filter);
}

internal static class LogParameters
{
    public const string PersistedGrantKey = "PersistedGrantKey";
    public const string PersistedGrantKeyFound = "PersistedGrantKeyFound";
    public const string PersistedGrantCount = "PersistedGrantCount";
    public const string Filter = "Filter";
}
