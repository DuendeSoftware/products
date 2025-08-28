// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Collections.Concurrent;
using System.Reflection.Metadata.Ecma335;
using System.Security.Claims;
using System.Security.Cryptography;
using Duende.Bff.AccessTokenManagement;
using Duende.Bff.Configuration;
using Duende.Bff.Internal;
using Duende.Bff.Otel;
using Duende.IdentityModel;
using Duende.Private.Licensing;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Duende.Bff.SessionManagement.Configuration;

/// <summary>
/// Cookie configuration to suppress sliding the cookie on the ~/bff/user endpoint if requested.
/// </summary>
internal class PostConfigureSlidingExpirationCheck(
    ActiveCookieAuthenticationScheme activeCookieScheme,
    IOptions<BffOptions> bffOptions,
    ILogger<PostConfigureSlidingExpirationCheck> logger)
    : IPostConfigureOptions<CookieAuthenticationOptions>
{
    private readonly BffOptions _options = bffOptions.Value;

    /// <inheritdoc />
    public void PostConfigure(string? name, CookieAuthenticationOptions options)
    {
        if (!activeCookieScheme.ShouldConfigureScheme(Scheme.ParseOrDefault(name)))
        {
            return;
        }

        options.Events.OnCheckSlidingExpiration = CreateCallback(options.Events.OnCheckSlidingExpiration);
    }

    private Func<CookieSlidingExpirationContext, Task> CreateCallback(Func<CookieSlidingExpirationContext, Task> inner)
    {
        Task Callback(CookieSlidingExpirationContext ctx)
        {
            var result = inner.Invoke(ctx);

            // disable sliding expiration
            if (ctx.HttpContext.Request.Path == _options.UserPath)
            {
                var slide = ctx.Request.Query[Constants.RequestParameters.SlideCookie];
                if (slide == "false")
                {
                    logger.SuppressingSlideBehaviorOnCheckSlidingExpiration(LogLevel.Debug);
                    ctx.ShouldRenew = false;
                }
            }

            return result;
        }

        return Callback;
    }
}

/// <summary>
/// 
/// </summary>
internal class PostConfigureTrialMode(
    ActiveCookieAuthenticationScheme activeCookieScheme,
    LicenseAccessor<BffLicense> license,
    TrialMode trialMode,
    ILogger<PostConfigureTrialMode> logger)
    : IPostConfigureOptions<CookieAuthenticationOptions>
{

    private volatile string[] _activeSessionIds = [];
    private readonly ConcurrentDictionary<string, bool> _sessionsToKill = [];

    /// <inheritdoc />
    public void PostConfigure(string? name, CookieAuthenticationOptions options)
    {
        if (!activeCookieScheme.ShouldConfigureScheme(Scheme.ParseOrDefault(name)))
        {
            return;
        }

        if (!license.Current.IsConfigured)
        {
            options.Events.OnSignedIn += ctx =>
            {
                var sid = ctx.Principal?.FindFirst(JwtClaimTypes.SessionId)?.Value;
                if (sid != null)
                {
                    trialMode.RecordSessionStarted(sid);
                }
                return Task.CompletedTask;
            };
            options.Events.OnSigningOut += ctx =>
            {
                var sid = ctx.HttpContext.User?.FindFirst(JwtClaimTypes.SessionId)?.Value;
                if (sid != null)
                {
                    trialMode.RecordSessionEnded(sid);
                }
                return Task.CompletedTask;
            };
        }
        
    }


}

internal class TrialMode
{
    private volatile string[] _activeSessionIds = [];
    private readonly ConcurrentDictionary<string, bool> _sessionsToKill = [];

    public void RecordSessionStarted(string sid)
    {
        var newSessions = _activeSessionIds.ToList();
        if (_activeSessionIds.Contains(sid))
        {
            newSessions.Remove(sid);
        }

        if (newSessions.Count >= Constants.LicenseEnforcement.MaximumNumberOfActiveSessionsInTrialMode)
        {
            var removed = newSessions[0];
            newSessions.RemoveAt(0);
            _sessionsToKill.TryAdd(removed, true);
        }

        newSessions.Add(sid);
        Interlocked.Exchange(ref _activeSessionIds, newSessions.ToArray());

    }

    public void RecordSessionEnded(string sid)
    {
        var newSessions = _activeSessionIds.ToList();
        if (_activeSessionIds.Contains(sid))
        {
            newSessions.Remove(sid);
        }

        Interlocked.Exchange(ref _activeSessionIds, newSessions.ToArray());
        _sessionsToKill.TryRemove(sid, out var _);
    }

    public bool ShouldEndSession(string sid)
    {
        return _sessionsToKill.ContainsKey(sid);
    }

}

internal class TrialModeVerificationMiddleware(TrialMode trialMode, RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var sid = context.User?.FindFirst(JwtClaimTypes.SessionId)?.Value;
        if (sid != null)
        {
            if (trialMode.ShouldEndSession(sid))
            {
                await context.SignOutAsync();
                return;
            }
        }
        await next(context);
    }
}
