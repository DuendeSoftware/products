// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.AccessTokenManagement;
using Duende.Bff.Internal;
using Duende.Bff.Licensing;
using Duende.IdentityModel;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Duende.Bff.SessionManagement.Configuration;

/// <summary>
/// Cookie configuration to check license validity when a user authenticates.
/// </summary>
internal class PostConfigureApplicationCookieTrialModeCheck(
    ActiveCookieAuthenticationScheme activeCookieScheme,
    LicenseValidator licenseValidator,
    TrialModeAuthenticatedSessionTracker authenticatedSessionTracker,
    ILogger<PostConfigureApplicationCookieTrialModeCheck> logger)
    : IPostConfigureOptions<CookieAuthenticationOptions>
{
    /// <inheritdoc />
    public void PostConfigure(string? name, CookieAuthenticationOptions options)
    {
        if (!activeCookieScheme.ShouldConfigureScheme(Scheme.ParseOrDefault(name)))
        {
            return;
        }

        if (!licenseValidator.CheckLicense())
        {
            options.Events.OnSigningIn = CreateCallback(options.Events.OnSigningIn);
        }
    }

    private Func<CookieSigningInContext, Task> CreateCallback(Func<CookieSigningInContext, Task> inner)
    {
        async Task Callback(CookieSigningInContext ctx)
        {
            var subjectId = ctx.Principal?.FindFirst(JwtClaimTypes.Subject)?.Value
                            ?? "unknown";

            authenticatedSessionTracker.RecordAuthenticatedSession(subjectId);

            if (authenticatedSessionTracker.UniqueAuthenticatedSessions >
                LicenseValidator.MaximumAllowedSessionsInTrialMode)
            {
                logger.TrialModeWarning(LogLevel.Error, LicenseValidator.MaximumAllowedSessionsInTrialMode);
            }

            await inner.Invoke(ctx);
        }

        return Callback;
    }
}
