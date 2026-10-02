// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Services.Default;

internal static partial class Log
{
    [LoggerMessage(
        LogLevel.Debug,
        EventName = nameof(HttpContextIsNullCannotStoreUiLocalesFor),
        Message = "HttpContext is null, cannot store ui_locales for redirect.")]
    internal static partial void HttpContextIsNullCannotStoreUiLocalesFor(this ILogger logger);

    [LoggerMessage(
        LogLevel.Debug,
        EventName = nameof(NoCookieRequestCultureProviderFoundCannotStoreUiLocalesFor),
        Message = "No CookieRequestCultureProvider found, cannot store ui_locales for redirect.")]
    internal static partial void NoCookieRequestCultureProviderFoundCannotStoreUiLocalesFor(this ILogger logger);

    [LoggerMessage(
        LogLevel.Debug,
        EventName = nameof(NoSupportedCultureFoundBasedOnValuesIn),
        Message = "No supported culture found based on values in ui_locales of {UiLocales}, not storing cookie.")]
    internal static partial void NoSupportedCultureFoundBasedOnValuesIn(this ILogger logger, object uiLocales);
}
