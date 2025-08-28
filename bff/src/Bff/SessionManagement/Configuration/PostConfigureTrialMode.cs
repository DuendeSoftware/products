// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Collections.Concurrent;
using Duende.Bff.AccessTokenManagement;
using Duende.Bff.Internal;
using Duende.Bff.Licensing;
using Duende.IdentityModel;
using Duende.Private.Licensing;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Duende.Bff.SessionManagement.Configuration;

/// <summary>
/// This post-configures cookie authentication to enforce trial mode limitations.
/// If the user has a valid license, then this turns into a no-op.
///
/// The goal of this class is to limit the number of active sessions in trial mode. It should not
/// be noticeable under normal trial / development usage because new sessions override older sessions.
/// 
/// This class does it's best to enforce the limit of active sessions in trial mode.
/// In a web farm scenario, the active session limit is only enforced on the server that
/// created the session.
///
/// In effect, this will NOT create a guaranteed block for users exceeding the limit in a webfarm
/// scenario. However, that's not the point. The fact that some requests will be blocked for users
/// should be enough to make a trial mode unreliable for production use.
///
/// Also note, that if the server is restarted, all session information is lost and previously
/// authenticated users can now use the system without issue. 
///
/// This solution may leak memory if users never sign out. The session IDs are stored in memory. However
/// since this is only for trial mode, it's not a big issue. A server restart will clear the memory.
/// 
/// This solution is not designed to be the most performant either. Trial mode means it's not suitable for
/// production usage. 
/// 
/// </summary>
internal class PostConfigureTrialMode(
    ActiveCookieAuthenticationScheme activeCookieScheme,
    LicenseAccessor<BffLicense> license,
    ILogger<PostConfigureTrialMode> logger)
    : IPostConfigureOptions<CookieAuthenticationOptions>
{

    private volatile string[] _activeSessionIds = [];
    private readonly ConcurrentDictionary<string, bool> _sessionsToKill = [];

    /// <inheritdoc />
    public void PostConfigure(string? name, CookieAuthenticationOptions options)
    {
        if (license.Current.IsConfigured)
        {
            // User has a valid license. 
            return;
        }

        if (!activeCookieScheme.ShouldConfigureScheme(Scheme.ParseOrDefault(name)))
        {
            return;
        }

        options.Events.OnSignedIn += AddSignedInUserToActiveSessionList;
        options.Events.OnSigningOut += RemoveSignedOutUserFromLists;
        options.Events.OnValidatePrincipal += PreventAccessForUsersExceedingLimit;

    }

    /// <summary>
    /// this method prevents access for sessions that have exceeded the maximum. Note,
    /// this only occurs on the server that actually created the session. 
    /// </summary>
    /// <param name="ctx"></param>
    /// <returns></returns>
    private Task PreventAccessForUsersExceedingLimit(CookieValidatePrincipalContext ctx)
    {
        var sid = ctx.Principal?.FindFirst(JwtClaimTypes.SessionId)?.Value;
        if (sid != null && _sessionsToKill.ContainsKey(sid))
        {
            logger.TrialModeRequestBlockedDueToTerminatedSession(LogLevel.Error, sid);
            ctx.RejectPrincipal();
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// When signing out, the currently logged in user is removed from the lists, so that
    /// it doesn't count towards the signed-in users count.
    /// </summary>
    /// <param name="ctx"></param>
    /// <returns></returns>
    private Task RemoveSignedOutUserFromLists(CookieSigningOutContext ctx)
    {
        var sid = ctx.HttpContext.User?.FindFirst(JwtClaimTypes.SessionId)?.Value;
        if (sid != null)
        {
            var newSessions = _activeSessionIds.ToList();
            if (_activeSessionIds.Contains(sid))
            {
                newSessions.Remove(sid);
            }
                    
            Interlocked.Exchange(ref _activeSessionIds, newSessions.ToArray());
            _sessionsToKill.TryRemove(sid, out var _);
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// Add a signed in user to the list of active users. If the number is exceeded, then
    /// mark the oldest as invalid. 
    /// </summary>
    /// <param name="ctx"></param>
    /// <returns></returns>
    private Task AddSignedInUserToActiveSessionList(CookieSignedInContext ctx)
    {
        var sid = ctx.Principal?.FindFirst(JwtClaimTypes.SessionId)?.Value;
        if (sid != null)
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
                logger.TrialModeSessionTerminated(LogLevel.Error, removed);
            }
            else
            {
                logger.TrialModeSessionStarted(LogLevel.Error, sid, newSessions.Count + 1);
            }

            newSessions.Add(sid);
            Interlocked.Exchange(ref _activeSessionIds, newSessions.ToArray());
        }
        return Task.CompletedTask;
    }
}
