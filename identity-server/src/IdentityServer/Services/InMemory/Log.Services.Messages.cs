// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Services;

internal static partial class Log
{
    [LoggerMessage(
        LogLevel.Debug,
        EventName = nameof(ClientListCheckedAndOriginValueIsAllowed),
        Message = "Client list checked and origin: {Value} is allowed")]
    internal static partial void ClientListCheckedAndOriginValueIsAllowed(this ILogger logger, object value);

    [LoggerMessage(
        LogLevel.Debug,
        EventName = nameof(ClientListCheckedAndOriginValueIsNot),
        Message = "Client list checked and origin: {Value} is not allowed")]
    internal static partial void ClientListCheckedAndOriginValueIsNot(this ILogger logger, object value);
}
