// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Configuration;

internal static partial class Log
{
    [LoggerMessage(
        LogLevel.Debug,
        EventName = nameof(LoginUrlUrl),
        Message = "Login Url: {Url}")]
    internal static partial void LoginUrlUrl(this ILogger logger, object url);

    [LoggerMessage(
        LogLevel.Debug,
        EventName = nameof(LoginReturnUrlParameterParam),
        Message = "Login Return Url Parameter: {Param}")]
    internal static partial void LoginReturnUrlParameterParam(this ILogger logger, object param);

    [LoggerMessage(
        LogLevel.Debug,
        EventName = nameof(LogoutUrlUrl),
        Message = "Logout Url: {Url}")]
    internal static partial void LogoutUrlUrl(this ILogger logger, object url);

    [LoggerMessage(
        LogLevel.Debug,
        EventName = nameof(ConsentUrlUrlUrl),
        Message = "ConsentUrl Url: {Url}")]
    internal static partial void ConsentUrlUrlUrl(this ILogger logger, object url);

    [LoggerMessage(
        LogLevel.Debug,
        EventName = nameof(ConsentReturnUrlParameterParam),
        Message = "Consent Return Url Parameter: {Param}")]
    internal static partial void ConsentReturnUrlParameterParam(this ILogger logger, object param);

    [LoggerMessage(
        LogLevel.Debug,
        EventName = nameof(ErrorUrlUrl),
        Message = "Error Url: {Url}")]
    internal static partial void ErrorUrlUrl(this ILogger logger, object url);

    [LoggerMessage(
        LogLevel.Debug,
        EventName = nameof(ErrorIdParameterParam),
        Message = "Error Id Parameter: {Param}")]
    internal static partial void ErrorIdParameterParam(this ILogger logger, object param);

    [LoggerMessage(
        LogLevel.Information,
        EventName = nameof(YouHaveEnabledTheOidcStateDataFormatterCacheButTheDistributed),
        Message = "You have enabled the OidcStateDataFormatterCache but the distributed cache registered is the default memory based implementation. This will store any OIDC state in memory on the server that initiated the request. If the response is processed on another server it will fail. If you are running in production, you want to switch to a real distributed cache that is shared between all nodes.")]
    internal static partial void YouHaveEnabledTheOidcStateDataFormatterCacheButTheDistributed(this ILogger logger);

    [LoggerMessage(
        LogLevel.Debug,
        EventName = nameof(FailedToConfigureServerSideSessionsForThe),
        Message = "Failed to configure server side sessions for the authentication cookie scheme \"{Scheme}\" because there is no current HTTP request")]
    internal static partial void FailedToConfigureServerSideSessionsForThe(this ILogger logger, object scheme);

    [LoggerMessage(
        LogLevel.Information,
        EventName = nameof(InMemoryServerSideSessionStoreInUse),
        Message = "You are using the in-memory version of the user session store. This will store user authentication sessions server side, but in memory only. If you are using this feature in production, you want to switch to a different store implementation.")]
    internal static partial void InMemoryServerSideSessionStoreInUse(this ILogger logger);
}
