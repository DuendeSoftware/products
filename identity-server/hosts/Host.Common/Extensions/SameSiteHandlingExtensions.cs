// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Host.Common.Extensions;

// copied from https://devblogs.microsoft.com/aspnet/upcoming-samesite-cookie-changes-in-asp-net-and-asp-net-core/
public static class SameSiteHandlingExtensions
{
    public static IServiceCollection AddSameSiteCookiePolicy(this IServiceCollection services)
    {
        services.Configure<CookiePolicyOptions>(options =>
        {
            options.MinimumSameSitePolicy = SameSiteMode.Unspecified;
            options.OnAppendCookie = cookieContext => CheckSameSite(cookieContext.Context, cookieContext.CookieOptions);
            options.OnDeleteCookie = cookieContext => CheckSameSite(cookieContext.Context, cookieContext.CookieOptions);
        });

        return services;
    }

    private static void CheckSameSite(HttpContext httpContext, CookieOptions options)
    {
        if (options.SameSite == SameSiteMode.None)
        {
            var userAgent = httpContext.Request.Headers.UserAgent.ToString();
            if (!httpContext.Request.IsHttps || DisallowsSameSiteNone(userAgent))
            {
                // For .NET Core < 3.1 set SameSite = (SameSiteMode)(-1)
                options.SameSite = SameSiteMode.Unspecified;
            }
        }
    }

    private static bool DisallowsSameSiteNone(string userAgent)
    {
        // Cover all iOS based browsers here. This includes:
        // - Safari on iOS 12 for iPhone, iPod Touch, iPad
        // - WkWebview on iOS 12 for iPhone, iPod Touch, iPad
        // - Chrome on iOS 12 for iPhone, iPod Touch, iPad
        // All of which are broken by SameSite=None, because they use the iOS networking stack
        if (userAgent.Contains("CPU iPhone OS 12", StringComparison.InvariantCulture) ||
            userAgent.Contains("iPad; CPU OS 12", StringComparison.InvariantCulture))
        {
            return true;
        }

        // Cover Mac OS X based browsers that use the Mac OS networking stack. This includes:
        // - Safari on Mac OS X.
        // This does not include:
        // - Chrome on Mac OS X
        // Because they do not use the Mac OS networking stack.
        if (userAgent.Contains("Macintosh; Intel Mac OS X 10_14", StringComparison.InvariantCulture) &&
            userAgent.Contains("Version/", StringComparison.InvariantCulture) &&
            userAgent.Contains("Safari", StringComparison.InvariantCulture))
        {
            return true;
        }

        // Cover Chrome 50-69, because some versions are broken by SameSite=None, 
        // and none in this range require it.
        // Note: this covers some pre-Chromium Edge versions, 
        // but pre-Chromium Edge does not require SameSite=None.
        if (userAgent.Contains("Chrome/5", StringComparison.InvariantCulture) ||
            userAgent.Contains("Chrome/6", StringComparison.InvariantCulture))
        {
            return true;
        }

        return false;
    }
}
