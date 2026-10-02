// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Stores.Storage;

internal static partial class Log
{
    [LoggerMessage(
        LogLevel.Debug,
        EventName = nameof(ClientListCheckedAndOriginOriginIsAllowed),
        Message = "Client list checked and origin: {Origin} is allowed")]
    internal static partial void ClientListCheckedAndOriginOriginIsAllowed(this ILogger logger, object origin);

    [LoggerMessage(
        LogLevel.Debug,
        EventName = nameof(ClientListCheckedAndOriginOriginIsNot),
        Message = "Client list checked and origin: {Origin} is not allowed")]
    internal static partial void ClientListCheckedAndOriginOriginIsNot(this ILogger logger, object origin);
}
