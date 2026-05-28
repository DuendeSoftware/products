// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Saml.ResponseHandling;

internal static partial class Log
{
    [LoggerMessage(
        EventName = nameof(ShowingLoginUserNotAuthenticated),
        Message = "Showing login: User is not authenticated")]
    internal static partial void ShowingLoginUserNotAuthenticated(this ILogger logger, LogLevel logLevel);

    [LoggerMessage(
        EventName = nameof(ShowingLoginServiceProviderContextMissing),
        Level = LogLevel.Warning,
        Message = "Showing login: Service provider context is missing")]
    internal static partial void ShowingLoginServiceProviderContextMissing(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(ShowingLoginUserNotActive),
        Message = "Showing login: User is not active")]
    internal static partial void ShowingLoginUserNotActive(this ILogger logger, LogLevel logLevel);
}
