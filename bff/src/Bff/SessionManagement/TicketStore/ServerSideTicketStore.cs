// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.Internal;
using Duende.IdentityModel;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;

// ReSharper disable once CheckNamespace
namespace Duende.Bff;

/// <summary>
/// IUserSession-backed ticket store
/// </summary>
public class ServerSideTicketStore(
    IUserSessionStore store,
    IDataProtectionProvider dataProtectionProvider,
    ILogger<ServerSideTicketStore> logger) : IServerTicketStore
{
    /// <summary>
    /// The "purpose" string to use when protecting and unprotecting server side
    /// tickets.
    /// </summary>
    public static string DataProtectorPurpose = "Duende.Bff.ServerSideTicketStore";

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
        logger.CreatingAuthenticationTicketEntry(key, ticket.GetExpiration());

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
    }

    /// <inheritdoc />
    public async Task<AuthenticationTicket?> RetrieveAsync(string key)
    {
        logger.RetrieveAuthenticationTicket(key);

        var session = await store.GetUserSessionAsync(key);
        if (session == null)
        {
            logger.NoAuthenticationTicketFoundForKey(key);
            return null;
        }

        var ticket = session.Deserialize(_protector, logger);
        if (ticket != null)
        {
            logger.LogDebug("Ticket loaded for key: {key}, with expiration: {expiration}", key, ticket.GetExpiration());
            return ticket;
        }

        // if we failed to get a ticket, then remove DB record 
        logger.FailedToDeserializeAuthenticationTicket(key);
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

        logger.RenewingAuthenticationTicket(key, ticket.GetExpiration());

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
        logger.RemovingAuthenticationTicket(key);

        return store.DeleteUserSessionAsync(key);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<AuthenticationTicket>> GetUserTicketsAsync(UserSessionsFilter filter, CancellationToken cancellationToken)
    {
        logger.GettingAuthenticationTickets(filter.SubjectId, filter.SessionId);

        var list = new List<AuthenticationTicket>();

        var sessions = await store.GetUserSessionsAsync(filter, cancellationToken);
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
                logger.FailedToDeserializeAuthenticationTicket(session.Key);
                await RemoveAsync(session.Key);
            }
        }

        return list;
    }
}
