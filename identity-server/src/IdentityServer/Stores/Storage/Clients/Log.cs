// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Stores.Storage.Clients;

internal static partial class Log
{
    [LoggerMessage(
        EventName = nameof(ClientFound),
        Message = $"{{{LogParameters.ClientId}}} found in database: {{{LogParameters.ClientIdFound}}}")]
    internal static partial void ClientFound(this ILogger logger, LogLevel logLevel, string clientId, bool clientIdFound);

    [LoggerMessage(
        EventName = nameof(ClientsRetrieved),
        Message = $"Retrieved {{{LogParameters.ClientCount}}} clients for enumeration")]
    internal static partial void ClientsRetrieved(this ILogger logger, LogLevel logLevel, int clientCount);
}

internal static class LogParameters
{
    public const string ClientId = "ClientId";
    public const string ClientIdFound = "ClientIdFound";
    public const string ClientCount = "ClientCount";
}
