// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using System.Globalization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Duende.IdentityServer.Services.Default;

public class DefaultUiLocalesService(IHttpContextAccessor httpContextAccessor, IOptions<RequestLocalizationOptions> requestLocalizationOptions, ILogger<DefaultUiLocalesService> logger) : IUiLocalesService
{
    public virtual Task StoreUiLocalesForRedirectAsync(string? uiLocales, CT ct)
    {
        if (httpContextAccessor.HttpContext is null)
        {
            logger.LogDebug("HttpContext is null, cannot store ui_locales for redirect.");

            return Task.CompletedTask;
        }

        var cookieRequestCultureProvider = requestLocalizationOptions.Value.RequestCultureProviders.OfType<CookieRequestCultureProvider>().FirstOrDefault();
        if (cookieRequestCultureProvider is null)
        {
            logger.LogDebug("No CookieRequestCultureProvider found, cannot store ui_locales for redirect.");
            return Task.CompletedTask;
        }

        var cultureCookieName = cookieRequestCultureProvider.CookieName;
        var firstSupportedCulture = GetFirstSupportedCulture(uiLocales);
        if (firstSupportedCulture is null)
        {
            logger.LogDebug("No supported culture found based on values in ui_locales of {ui_locales}, not storing cookie.", uiLocales);
            return Task.CompletedTask;
        }

        var cookieValue = CookieRequestCultureProvider.MakeCookieValue(firstSupportedCulture);
        httpContextAccessor.HttpContext.Response.Cookies.Append(cultureCookieName, cookieValue);

        return Task.CompletedTask;
    }

    protected virtual RequestCulture? GetFirstSupportedCulture(string? uiLocales)
    {
        if (string.IsNullOrWhiteSpace(uiLocales) || requestLocalizationOptions.Value.SupportedUICultures?.Count == 0)
        {
            return null;
        }

        var uiLocalesParts = uiLocales.Split([' '], StringSplitOptions.RemoveEmptyEntries);
        CultureInfo? matchedCulture = null;
        foreach (var uiLocale in uiLocalesParts)
        {
            var supportedCulture = requestLocalizationOptions.Value.SupportedUICultures?.FirstOrDefault(c => c.Name.Equals(uiLocale, StringComparison.Ordinal));
            if (supportedCulture is null)
            {
                continue;
            }

            matchedCulture = supportedCulture;
            break;
        }

        return matchedCulture == null ? null : new RequestCulture(matchedCulture);
    }
}
