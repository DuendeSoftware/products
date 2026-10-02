// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Hosting.LocalApiAuthentication;

internal static partial class Log
{
    [LoggerMessage(
        LogLevel.Trace,
        EventName = nameof(HandleAuthenticateAsyncCalled),
        Message = "HandleAuthenticateAsync called")]
    internal static partial void HandleAuthenticateAsyncCalled(this ILogger logger);

    [LoggerMessage(
        LogLevel.Trace,
        EventName = nameof(BearerTokenSentButModeIsDPoPOnly),
        Message = "Bearer token sent, but mode is DPoP only. Ignoring token.")]
    internal static partial void BearerTokenSentButModeIsDPoPOnly(this ILogger logger);

    [LoggerMessage(
        LogLevel.Trace,
        EventName = nameof(DPoPTokenSentButModeIsBearerOnly),
        Message = "DPoP token sent, but mode is Bearer only. Ignoring token.")]
    internal static partial void DPoPTokenSentButModeIsBearerOnly(this ILogger logger);

    [LoggerMessage(
        LogLevel.Trace,
        EventName = nameof(TokenFoundToken),
        Message = "Token found: {Token}")]
    internal static partial void TokenFoundToken(this ILogger logger, object token);

    [LoggerMessage(
        LogLevel.Trace,
        EventName = nameof(FailedToValidateTheToken),
        Message = "Failed to validate the token")]
    internal static partial void FailedToValidateTheToken(this ILogger logger);

    [LoggerMessage(
        LogLevel.Debug,
        EventName = nameof(LocalApiDPoPProofTokenIsTooLong),
        Message = "DPoP proof token is too long")]
    internal static partial void LocalApiDPoPProofTokenIsTooLong(this ILogger logger);

    [LoggerMessage(
        LogLevel.Trace,
        EventName = nameof(SuccessfullyValidatedTheToken),
        Message = "Successfully validated the token.")]
    internal static partial void SuccessfullyValidatedTheToken(this ILogger logger);
}
