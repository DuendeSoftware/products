// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.Otel;
using Duende.Bff.SessionManagement.SessionStore;
using Duende.IdentityModel;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;

namespace Duende.Bff.SessionManagement.TicketStore;

/// <summary>
/// IUserSession-backed ticket store
/// </summary>
internal class ServerSideTicketStore(
    BffMetrics metrics,
    IUserSessionStore store,
    IDataProtectionProvider dataProtectionProvider,
    ILogger<ServerSideTicketStore> logger) : IServerTicketStore
{
    /// <summary>
    /// The "purpose" string to use when protecting and deprotecting server side
    /// tickets.
    /// </summary>
    public const string DataProtectorPurpose = "Duende.Bff.ServerSideTicketStore";

    private readonly IDataProtector _protector = dataProtectionProvider.CreateProtector(DataProtectorPurpose);

    /// <inheritdoc />
    public async Task<string> StoreAsync(AuthenticationTicket ticket)
    {
        // it's possible that the user re-triggered OIDC (somehow) prior to
        // the session DB records being cleaned up, so we should preemptively remove
        // conflicting session records for this sub/sid combination
        await store.DeleteUserSessionsAsync(new UserSessionsFilter
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
        logger.CreatingAuthenticationTicketEntry(LogLevel.Debug, key, ticket.GetExpiration());

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

        await store.CreateUserSessionAsync(session);
        metrics.SessionStarted();
    }

    /// <inheritdoc />
    public async Task<AuthenticationTicket?> RetrieveAsync(string key)
    {
        logger.RetrieveAuthenticationTicket(LogLevel.Debug, key);

        var session = await store.GetUserSessionAsync(key);
        if (session == null)
        {
            logger.NoAuthenticationTicketFoundForKey(LogLevel.Debug, key);
            return null;
        }

        var ticket = session.Deserialize(_protector, logger);
        if (ticket != null)
        {
            logger.TicketLoaded(LogLevel.Debug, key, ticket.GetExpiration());
            return ticket;
        }

        // if we failed to get a ticket, then remove DB record 
        logger.FailedToDeserializeAuthenticationTicket(LogLevel.Information, key);
        await RemoveAsync(key);
        return ticket;
    }

    /// <inheritdoc />
    public async Task RenewAsync(string key, AuthenticationTicket ticket)
    {
        var session = await store.GetUserSessionAsync(key);
        if (session == null)
        {
            // https://github.com/dotnet/aspnetcore/issues/41516#issuecomment-1178076544
            await CreateNewSessionAsync(key, ticket);
            return;
        }

        logger.RenewingAuthenticationTicket(LogLevel.Debug, key, ticket.GetExpiration());

        var sub = ticket.GetSubjectId();
        var sid = ticket.GetSessionId();
        var isNew = session.SubjectId != sub || session.SessionId != sid;
        var created = isNew ? ticket.GetIssued() : session.Created;

        await store.UpdateUserSessionAsync(key, new UserSessionUpdate
        {
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
        logger.RemovingAuthenticationTicket(LogLevel.Debug, key);
        metrics.SessionEnded();

        return store.DeleteUserSessionAsync(key);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<AuthenticationTicket>> GetUserTicketsAsync(UserSessionsFilter filter, CT ct)
    {
        logger.GettingAuthenticationTickets(LogLevel.Debug, filter.SubjectId, filter.SessionId);

        var list = new List<AuthenticationTicket>();

        var sessions = await store.GetUserSessionsAsync(filter, ct);
        foreach (var session in sessions)
        {
            var ticket = session.Deserialize(_protector, logger);
            if (ticket != null)
            {
                list.Add(ticket);
            }
            else
            {
                // if we failed to get a ticket, then remove DB record 
                logger.FailedToDeserializeAuthenticationTicket(LogLevel.Debug, session.Key);
                await RemoveAsync(session.Key);
            }
        }

        return list;
    }
}
