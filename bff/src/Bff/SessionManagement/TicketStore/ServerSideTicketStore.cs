// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable disable

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Duende.IdentityModel;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;

namespace Duende.Bff;

/// <summary>
/// IUserSession-backed ticket store
/// </summary>
public class ServerSideTicketStore : IServerTicketStore
{
    /// <summary>
    /// The "purpose" string to use when protecting and unprotecting server side
    /// tickets.
    /// </summary>
    public static string DataProtectorPurpose = "Duende.Bff.ServerSideTicketStore";

    private readonly IUserSessionStore _store;
    private readonly IDataProtector _protector;
    private readonly ILogger<ServerSideTicketStore> _logger;

    /// <summary>
    /// ctor
    /// </summary>
    /// <param name="store"></param>
    /// <param name="dataProtectionProvider"></param>
    /// <param name="logger"></param>
    public ServerSideTicketStore(
        IUserSessionStore store,
        IDataProtectionProvider dataProtectionProvider,
        ILogger<ServerSideTicketStore> logger)
    {
        _store = store;
        _protector = dataProtectionProvider.CreateProtector(DataProtectorPurpose);
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<string> StoreAsync(AuthenticationTicket ticket)
    {
        // it's possible that the user re-triggered OIDC (somehow) prior to
        // the session DB records being cleaned up, so we should preemptively remove
        // conflicting session records for this sub/sid combination
        await _store.DeleteUserSessionsAsync(new UserSessionsFilter
        {
            SubjectId = ticket.GetSubjectId(),
            SessionId = ticket.GetSessionId()
        });

        var key = CryptoRandom.CreateUniqueId(format: CryptoRandom.OutputFormat.Hex);

        await CreateNewSessionAsync(key, ticket);

        return key;
    }

    private async Task CreateNewSessionAsync(string key, AuthenticationTicket ticket)
    {
        _logger.LogDebug("Creating entry in store for AuthenticationTicket, key {key}, with expiration: {expiration}", key, ticket.GetExpiration());

        var session = new UserSession
        {
            Key = key,
            Created = ticket.GetIssued(),
            Renewed = ticket.GetIssued(),
            Expires = ticket.GetExpiration(),
            SubjectId = ticket.GetSubjectId(),
            SessionId = ticket.GetSessionId(),
            Ticket = ticket.Serialize(_protector)
        };

        await _store.CreateUserSessionAsync(session);
    }

    /// <inheritdoc />
    public async Task<AuthenticationTicket> RetrieveAsync(string key)
    {
        _logger.LogDebug("Retrieve AuthenticationTicket for key {key}", key);

        var session = await _store.GetUserSessionAsync(key);
        if (session == null)
        {
            _logger.LogDebug("No ticket found in store for {key}", key);
            return null;
        }
            
        var ticket = session.Deserialize(_protector, _logger);
        if (ticket != null)
        {
            _logger.LogDebug("Ticket loaded for key: {key}, with expiration: {expiration}", key, ticket.GetExpiration());
            return ticket;
        }

        // if we failed to get a ticket, then remove DB record 
        _logger.LogWarning("Failed to deserialize authentication ticket from store, deleting record for key {key}", key);
        await RemoveAsync(key);

        return ticket;
    }

    /// <inheritdoc />
    public async Task RenewAsync(string key, AuthenticationTicket ticket)
    {
        var session = await _store.GetUserSessionAsync(key);
        if (session == null)
        {
            // https://github.com/dotnet/aspnetcore/issues/41516#issuecomment-1178076544
            await CreateNewSessionAsync(key, ticket);
            return;
        }

        _logger.LogDebug("Renewing AuthenticationTicket for key {key}, with expiration: {expiration}", key, ticket.GetExpiration());

        var sub = ticket.GetSubjectId();
        var sid = ticket.GetSessionId();
        var isNew = session.SubjectId != sub || session.SessionId != sid;
        var created = isNew ? ticket.GetIssued() : session.Created;

        await _store.UpdateUserSessionAsync(key, new UserSessionUpdate {
            SubjectId = ticket.GetSubjectId(),
            SessionId = ticket.GetSessionId(),
            Created = created,
            Renewed = ticket.GetIssued(),
            Expires = ticket.GetExpiration(),
            Ticket = ticket.Serialize(_protector)
        });
    }

    /// <inheritdoc />
    public Task RemoveAsync(string key)
    {
        _logger.LogDebug("Removing AuthenticationTicket from store for key {key}", key);

        return _store.DeleteUserSessionAsync(key);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<AuthenticationTicket>> GetUserTicketsAsync(UserSessionsFilter filter, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Getting AuthenticationTickets from store for sub {sub} sid {sid}", filter.SubjectId, filter.SessionId);
        
        var list = new List<AuthenticationTicket>();
            
        var sessions = await _store.GetUserSessionsAsync(filter, cancellationToken);
        foreach(var session in sessions)
        {
            var ticket = session.Deserialize(_protector, _logger);
            if (ticket != null)
            {
                list.Add(ticket);
            }
            else
            {
                // if we failed to get a ticket, then remove DB record 
                _logger.LogWarning("Failed to deserialize authentication ticket from store, deleting record for key {key}", session.Key);
                await RemoveAsync(session.Key);
            }
        }

        return list;
    }
}