// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;

namespace Microsoft.AspNetCore.Builder;

internal static partial class Log
{
    [LoggerMessage(
        LogLevel.Information,
        EventName = nameof(StartingDuendeIdentityServerVersionVersionNetversion),
        Message = "Starting Duende IdentityServer version {Version} ({Netversion})")]
    internal static partial void StartingDuendeIdentityServerVersionVersionNetversion(this ILogger logger, object version, object netversion);

    [LoggerMessage(
        LogLevel.Information,
        EventName = nameof(InMemoryPersistedGrantStoreInUse),
        Message = "You are using the in-memory version of the persisted grant store. This will store consent decisions, authorization codes, refresh and reference tokens in memory only. If you are using any of those features in production, you want to switch to a different store implementation.")]
    internal static partial void InMemoryPersistedGrantStoreInUse(this ILogger logger);

    [LoggerMessage(
        LogLevel.Warning,
        EventName = nameof(NoAuthenticationSchemeHasBeenSetSettingEither),
        Message = "No authentication scheme has been set. Setting either a default authentication scheme or a CookieAuthenticationScheme on IdentityServerOptions is required.")]
    internal static partial void NoAuthenticationSchemeHasBeenSetSettingEither(this ILogger logger);

    [LoggerMessage(
        LogLevel.Information,
        EventName = nameof(UsingExplicitlyConfiguredAuthenticationSchemeSchemeForIdentityServer),
        Message = "Using explicitly configured authentication scheme {Scheme} for IdentityServer")]
    internal static partial void UsingExplicitlyConfiguredAuthenticationSchemeSchemeForIdentityServer(this ILogger logger, object scheme);

    [LoggerMessage(
        LogLevel.Information,
        EventName = nameof(UsingTheDefaultAuthenticationSchemeSchemeForIdentityServer),
        Message = "Using the default authentication scheme {Scheme} for IdentityServer")]
    internal static partial void UsingTheDefaultAuthenticationSchemeSchemeForIdentityServer(this ILogger logger, object scheme);

    [LoggerMessage(
        LogLevel.Information,
        EventName = nameof(AuthenticationSchemeSchemeIsConfiguredForIdentityServerBut),
        Message = "Authentication scheme {Scheme} is configured for IdentityServer, but it is not a scheme that supports signin (like cookies). If you support interactive logins via the browser, then a cookie-based scheme should be used.")]
    internal static partial void AuthenticationSchemeSchemeIsConfiguredForIdentityServerBut(this ILogger logger, object scheme);

    [LoggerMessage(
        LogLevel.Debug,
        EventName = nameof(UsingSchemeAsDefaultASPNETCoreScheme),
        Message = "Using {Scheme} as default ASP.NET Core scheme for authentication")]
    internal static partial void UsingSchemeAsDefaultASPNETCoreScheme(this ILogger logger, object scheme);

    [LoggerMessage(
        LogLevel.Debug,
        EventName = nameof(UsingSchemeAsDefaultASPNETCoreScheme2),
        Message = "Using {Scheme} as default ASP.NET Core scheme for sign-in")]
    internal static partial void UsingSchemeAsDefaultASPNETCoreScheme2(this ILogger logger, object scheme);

    [LoggerMessage(
        LogLevel.Debug,
        EventName = nameof(UsingSchemeAsDefaultASPNETCoreScheme3),
        Message = "Using {Scheme} as default ASP.NET Core scheme for sign-out")]
    internal static partial void UsingSchemeAsDefaultASPNETCoreScheme3(this ILogger logger, object scheme);

    [LoggerMessage(
        LogLevel.Debug,
        EventName = nameof(UsingSchemeAsDefaultASPNETCoreScheme4),
        Message = "Using {Scheme} as default ASP.NET Core scheme for challenge")]
    internal static partial void UsingSchemeAsDefaultASPNETCoreScheme4(this ILogger logger, object scheme);

    [LoggerMessage(
        LogLevel.Debug,
        EventName = nameof(UsingSchemeAsDefaultASPNETCoreScheme5),
        Message = "Using {Scheme} as default ASP.NET Core scheme for forbid")]
    internal static partial void UsingSchemeAsDefaultASPNETCoreScheme5(this ILogger logger, object scheme);

    [LoggerMessage(
        LogLevel.Debug,
        EventName = nameof(CustomIssuerUriSetToValue),
        Message = "Custom IssuerUri set to {Value}")]
    internal static partial void CustomIssuerUriSetToValue(this ILogger logger, object value);

    [LoggerMessage(
        LogLevel.Critical,
        EventName = nameof(Message),
        Message = "{Message}")]
    internal static partial void Message(this ILogger logger, object message);
}
