// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Configuration;

internal static partial class Log
{
    [LoggerMessage(
        EventName = nameof(DynamicClientRegistrationRequestBodyNull),
        Level = LogLevel.Debug,
        Message = "Dynamic client registration request body cannot be null")]
    internal static partial void DynamicClientRegistrationRequestBodyNull(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(DynamicClientRegistrationRequestBodyParseFailed),
        Level = LogLevel.Debug,
        Message = "Failed to parse dynamic client registration request body")]
    internal static partial void DynamicClientRegistrationRequestBodyParseFailed(this ILogger logger, Exception ex);

    [LoggerMessage(
        EventName = nameof(InvalidDynamicClientRegistrationContentType),
        Level = LogLevel.Debug,
        Message = "Invalid content type in dynamic client registration request")]
    internal static partial void InvalidDynamicClientRegistrationContentType(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(OfflineAccessScopeIgnored),
        Level = LogLevel.Debug,
        Message = "offline_access should not be passed as a scope to dynamic client registration. Use the refresh_token grant_type instead.")]
    internal static partial void OfflineAccessScopeIgnored(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(NoDefaultDynamicClientRegistrationScopes),
        Level = LogLevel.Debug,
        Message = "No scopes requested for dynamic client registration, and no default scope behavior implemented. To set default scopes, extend the DynamicClientRegistrationValidator and override the SetDefaultScopes method.")]
    internal static partial void NoDefaultDynamicClientRegistrationScopes(this ILogger logger);

    [LoggerMessage(
        EventName = nameof(JwkParseFailed),
        Level = LogLevel.Error,
        Message = "Failed to parse jwk")]
    internal static partial void JwkParseFailed(this ILogger logger, Exception ex);
}
