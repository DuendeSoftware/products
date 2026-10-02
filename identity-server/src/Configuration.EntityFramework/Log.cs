// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Configuration;

internal static partial class Log
{
    [LoggerMessage(
        EventName = nameof(AddingClient),
        Level = LogLevel.Debug,
        Message = "Adding client {ClientId} to configuration store")]
    internal static partial void AddingClient(this ILogger logger, string clientId);
}
