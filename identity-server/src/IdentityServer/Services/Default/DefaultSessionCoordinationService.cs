// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Duende.IdentityServer.Services;

/// <summary>
/// Default session coordination service
/// </summary>
public class DefaultSessionCoordinationService : ISessionCoordinationService
{
    /// <summary>
    /// The options.
    /// </summary>
    protected readonly IdentityServerOptions Options;

    /// <summary>
    /// The persisted grant store.
    /// </summary>
    protected readonly IPersistedGrantStore PersistedGrantStore;

    /// <summary>
    /// The client store.
    /// </summary>
    protected readonly IClientStore ClientStore;

    /// <summary>
    /// The back-channel logout service.
    /// </summary>
    protected readonly IBackChannelLogoutService BackChannelLogoutService;

    /// <summary>
    /// The logger.
    /// </summary>
    protected readonly ILogger Logger;

    /// <summary>
    /// The server-side session store (if configured).
    /// </summary>
    protected readonly IServerSideSessionStore ServerSideSessionStore;
    
    /// <summary>
    /// Ctor.
    /// </summary>
    public DefaultSessionCoordinationService(
        IdentityServerOptions options,
        IPersistedGrantStore persistedGrantStore,
        IClientStore clientStore,
        IBackChannelLogoutService backChannelLogoutService,
        ILogger<DefaultSessionCoordinationService> logger,
        IServerSideSessionStore serverSideSessionStore = null)
    {
        Options = options;
        PersistedGrantStore = persistedGrantStore;
        ClientStore = clientStore;
        BackChannelLogoutService = backChannelLogoutService;
        Logger = logger;
        ServerSideSessionStore = serverSideSessionStore;
    }

    /// <summary>
    /// The persisted grants that are token types.
    /// </summary>
    protected static readonly string[] PersistedGrantTokenTypes = new[] {
        IdentityServerConstants.PersistedGrantTypes.RefreshToken,
        IdentityServerConstants.PersistedGrantTypes.ReferenceToken,
        IdentityServerConstants.PersistedGrantTypes.AuthorizationCode,
        IdentityServerConstants.PersistedGrantTypes.BackChannelAuthenticationRequest,
    };

    /// <inheritdoc/>
    public virtual async Task ProcessLogoutAsync(UserSession session)
    {
        if (session.ClientIds.Count > 0)
        {
            var clientsToCoordinate = new List<string>();
            foreach (var clientId in session.ClientIds)
            {
                var client = await ClientStore.FindClientByIdAsync(clientId); // i don't think we care if it's an enabled client at this point
                if (client != null)
                {
                    var shouldCoordinate =
                        client.CoordinateLifetimeWithUserSession == true ||
                        (Options.Authentication.CoordinateClientLifetimesWithUserSession && client.CoordinateLifetimeWithUserSession != false);

                    if (shouldCoordinate)
                    {
                        clientsToCoordinate.Add(clientId);
                    }
                }
            }

            if (clientsToCoordinate.Count > 0)
            {
                Logger.LogDebug("Due to user logout, removing tokens for subject id {subjectId} and session id {sessionId}", session.SubjectId, session.SessionId);
                
                await PersistedGrantStore.RemoveAllAsync(new PersistedGrantFilter
                {
                    SubjectId = session.SubjectId,
                    SessionId = session.SessionId,
                    ClientIds = clientsToCoordinate,
                    Types = PersistedGrantTokenTypes
                });
            }

            Logger.LogDebug("Due to user logout, invoking backchannel logout for subject id {subjectId} and session id {sessionId}", session.SubjectId, session.SessionId);
            
            // this uses all the clientIds since that's how logout worked before session coordination existed
            // IOW, we know we're not using the clientsToCoordinate list here, also because it's active logout
            await BackChannelLogoutService.SendLogoutNotificationsAsync(new LogoutNotificationContext
            {
                SubjectId = session.SubjectId,
                SessionId = session.SessionId,
                ClientIds = session.ClientIds,
                Issuer = session.Issuer,
                LogoutReason = LogoutNotificationReason.UserLogout
            });
        }
    }


    /// <inheritdoc/>
    public virtual async Task ProcessExpirationAsync(UserSession session)
    {
        var clientsToCoordinate = new List<string>();

        foreach (var clientId in session.ClientIds)
        {
            var client = await ClientStore.FindClientByIdAsync(clientId); // i don't think we care if it's an enabled client at this point

            if (client != null)
            {
                var shouldCoordinate =
                    client.CoordinateLifetimeWithUserSession == true ||
                    (Options.Authentication.CoordinateClientLifetimesWithUserSession && client.CoordinateLifetimeWithUserSession != false);

                if (shouldCoordinate)
                {
                    // this implies they should also be contacted for backchannel logout below
                    clientsToCoordinate.Add(clientId);
                }
            }
        }

        if (clientsToCoordinate.Count > 0)
        {
            Logger.LogDebug("Due to expired session, removing tokens for subject id {subjectId} and session id {sessionId}", session.SubjectId, session.SessionId);
            
            await PersistedGrantStore.RemoveAllAsync(new PersistedGrantFilter
            {
                SubjectId = session.SubjectId,
                SessionId = session.SessionId,
                Types = PersistedGrantTokenTypes,
                ClientIds = clientsToCoordinate
            });
        }

        if (Options.ServerSideSessions.ExpiredSessionsTriggerBackchannelLogout || clientsToCoordinate.Count > 0)
        {
            var clientsToContact = session.ClientIds;
            if (Options.ServerSideSessions.ExpiredSessionsTriggerBackchannelLogout == false)
            {
                // the global setting is not enabled, so filter on those specific clients configured
                clientsToContact = clientsToContact.Intersect(clientsToCoordinate).ToList();
            }

            if (clientsToContact.Count > 0)
            {
                Logger.LogDebug("Due to expired session, invoking backchannel logout for subject id {subjectId} and session id {sessionId}", session.SubjectId, session.SessionId);

                await BackChannelLogoutService.SendLogoutNotificationsAsync(new LogoutNotificationContext
                {
                    SubjectId = session.SubjectId,
                    SessionId = session.SessionId,
                    Issuer = session.Issuer,
                    ClientIds = clientsToContact,
                    LogoutReason = LogoutNotificationReason.SessionExpiration,
                });
            }
        }
    }


    /// <inheritdoc/>
    public virtual async Task<bool> ValidateSessionAsync(SessionValidationRequest request)
    {
        if (ServerSideSessionStore != null)
        {
            var shouldCoordinate =
                request.Client.CoordinateLifetimeWithUserSession == true ||
                (Options.Authentication.CoordinateClientLifetimesWithUserSession && request.Client.CoordinateLifetimeWithUserSession != false);

            if (shouldCoordinate)
            {
                var sessions = await ServerSideSessionStore.GetSessionsAsync(new SessionFilter
                {
                    SubjectId = request.SubjectId,
                    SessionId = request.SessionId
                });

                var valid = sessions.Count > 0 &&
                    sessions.Any(x => x.Expires == null || DateTime.UtcNow < x.Expires.Value);

                if (!valid)
                {
                    Logger.LogDebug("Due to missing/expired server-side session, failing token validation for subject id {subjectId} and session id {sessionId}", request.SubjectId, request.SessionId);
                    return false;
                }

                Logger.LogDebug("Due to client token use, extending server-side session for subject id {subjectId} and session id {sessionId}", request.SubjectId, request.SessionId);

                foreach (var session in sessions)
                {
                    if (session.Expires.HasValue)
                    {
                        // setting the Expires flag on the entity (and not in the AuthenticationTicket)
                        // since we know that when loading from the DB that column will overwrite the 
                        // expires in the AuthenticationTicket.
                        var diff = session.Expires.Value.Subtract(session.Renewed);
                        session.Renewed = DateTime.UtcNow;
                        session.Expires = session.Renewed.Add(diff);

                        await ServerSideSessionStore.UpdateSessionAsync(session);
                    }
                }
            }
        }

        return true;
    }
}
