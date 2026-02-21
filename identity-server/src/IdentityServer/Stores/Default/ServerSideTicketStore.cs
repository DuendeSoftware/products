// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityModel;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Stores;

/// <summary>
/// IServerSideSessionService backed by server side session store
/// </summary>
public class ServerSideTicketStore : IServerSideTicketStore
{
    private readonly IdentityServerOptions _options;
    private readonly IIssuerNameService _issuerNameService;
    private readonly IServerSideSessionStore _store;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IDataProtector _protector;
    private readonly ILogger<ServerSideTicketStore> _logger;

    /// <summary>
    /// ctor
    /// </summary>
    /// <param name="options"></param>
    /// <param name="issuerNameService"></param>
    /// <param name="store"></param>
    /// <param name="dataProtectionProvider"></param>
    /// <param name="httpContextAccessor"></param>
    /// <param name="logger"></param>
    public ServerSideTicketStore(
        IdentityServerOptions options,
        IIssuerNameService issuerNameService,
        IServerSideSessionStore store,
        IDataProtectionProvider dataProtectionProvider,
        IHttpContextAccessor httpContextAccessor,
        ILogger<ServerSideTicketStore> logger)
    {
        _options = options;
        _issuerNameService = issuerNameService;
        _store = store;
        _httpContextAccessor = httpContextAccessor;
        _protector = dataProtectionProvider.CreateProtector("Duende.SessionManagement.ServerSideTicketStore");
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<string> StoreAsync(AuthenticationTicket ticket)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("ServerSideTicketStore.Store");

        ArgumentNullException.ThrowIfNull(ticket);

        ticket.SetIssuer(await _issuerNameService.GetCurrentAsync(default));

        var key = CryptoRandom.CreateUniqueId(format: CryptoRandom.OutputFormat.Hex);

        await CreateNewSessionAsync(key, ticket);

        return key;
    }

    private async Task CreateNewSessionAsync(string key, AuthenticationTicket ticket)
    {
        _logger.LogDebug("Creating entry in store for AuthenticationTicket, key {key}, with expiration: {expiration}", key, ticket.GetExpiration());

        var session = new ServerSideSession
        {
            Key = key,
            Scheme = ticket.AuthenticationScheme,
            Created = ticket.GetIssued(),
            Renewed = ticket.GetIssued(),
            Expires = ticket.GetExpiration(),
            SubjectId = ticket.GetSubjectId(),
            SessionId = ticket.GetSessionId(),
            DisplayName = ticket.GetDisplayName(_options.ServerSideSessions.UserDisplayNameClaimType),
            Ticket = ticket.Serialize(_protector)
        };

        await _store.CreateSessionAsync(session, _httpContextAccessor.HttpContext?.RequestAborted ?? default);
    }

    /// <inheritdoc />
    public async Task<AuthenticationTicket> RetrieveAsync(string key)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("ServerSideTicketStore.Retrieve");

        ArgumentNullException.ThrowIfNull(key);

        _logger.LogDebug("Retrieve AuthenticationTicket for key {key}", key);

        var session = await _store.GetSessionAsync(key, _httpContextAccessor.HttpContext?.RequestAborted ?? default);
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
        using var activity = Tracing.StoreActivitySource.StartActivity("ServerSideTicketStore.Renew");

        ArgumentNullException.ThrowIfNull(ticket);

        var session = await _store.GetSessionAsync(key, _httpContextAccessor.HttpContext?.RequestAborted ?? default);
        if (session == null)
        {
            // https://github.com/dotnet/aspnetcore/issues/41516#issuecomment-1178076544
            await CreateNewSessionAsync(key, ticket);
            return;
        }

        _logger.LogDebug("Renewing AuthenticationTicket for key {key}, with expiration: {expiration}", key, ticket.GetExpiration());

        var sub = ticket.GetSubjectId();
        var sid = ticket.GetSessionId();
        var name = string.IsNullOrWhiteSpace(_options.ServerSideSessions.UserDisplayNameClaimType) ? null : ticket.Principal.FindFirst(_options.ServerSideSessions.UserDisplayNameClaimType)?.Value;

        var isNew = session.SubjectId != sub || session.SessionId != sid;
        if (isNew)
        {
            session.Created = ticket.GetIssued();
            session.SubjectId = sub;
            session.SessionId = sid;
        }

        if (ticket.GetIssuer() == null)
        {
            // when issuing a new cookie on top of an existing cookie, the AuthenticationTicket passed above is new (and not the prior one loaded from the ticket store)
            ticket.SetIssuer(await _issuerNameService.GetCurrentAsync(default));
        }
        session.Renewed = ticket.GetIssued();
        session.Expires = ticket.GetExpiration();
        session.DisplayName = name;
        session.Ticket = ticket.Serialize(_protector);

        await _store.UpdateSessionAsync(session, _httpContextAccessor.HttpContext?.RequestAborted ?? default);
    }

    /// <inheritdoc />
    public async Task RemoveAsync(string key)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("ServerSideTicketStore.Remove");

        ArgumentNullException.ThrowIfNull(key);

        _logger.LogDebug("Removing AuthenticationTicket from store for key {key}", key);

        // There is a somewhat rare scenario where a session has expired and a request to IdentityServer happens prior
        // to the cleanup job running. When that happens, the session is removed but none of the processing to trigger
        // backchannel logouts, etc. happens so we need a way to kick that off and are doing so here.
        var session = await _store.GetSessionAsync(key, _httpContextAccessor.HttpContext?.RequestAborted ?? default);
        if (session != null)
        {
            var userSession = AsUserSessions([session]).SingleOrDefault();
            if (userSession != null)
            {
                _httpContextAccessor.HttpContext?.SetExpiredUserSession(userSession);
            }
        }

        await _store.DeleteSessionAsync(key, _httpContextAccessor.HttpContext?.RequestAborted ?? default);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<UserSession>> GetSessionsAsync(SessionFilter filter, CT ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("ServerSideTicketStore.GetSessions");

        var sessions = await _store.GetSessionsAsync(filter, ct);

        return AsUserSessions(sessions);

    }

    /// <inheritdoc />
    public async Task<QueryResult<UserSession>> QuerySessionsAsync(SessionQuery filter, CT ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("ServerSideTicketStore.QuerySessions");

        var results = await _store.QuerySessionsAsync(ct, filter);

        var tickets = AsUserSessions(results.Results);

        var result = new QueryResult<UserSession>
        {
            ResultsToken = results.ResultsToken,
            HasPrevResults = results.HasPrevResults,
            HasNextResults = results.HasNextResults,
            TotalCount = results.TotalCount,
            TotalPages = results.TotalPages,
            CurrentPage = results.CurrentPage,
            Results = tickets.ToArray(),
        };

        return result;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<UserSession>> GetAndRemoveExpiredSessionsAsync(int count, CT ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("ServerSideTicketStore.GetAndRemoveExpiredSessions");

        var sessions = await _store.GetAndRemoveExpiredSessionsAsync(count, ct);

        return AsUserSessions(sessions);
    }

    private UserSession[] AsUserSessions(IEnumerable<ServerSideSession> sessions) => sessions
            .Select(x => new { x.Created, Ticket = x.Deserialize(_protector, _logger)! })
            .Where(x => x != null && x.Ticket != null)
            .Select(item => new UserSession
            {
                SubjectId = item.Ticket.GetSubjectId(),
                SessionId = item.Ticket.GetSessionId(),
                DisplayName = item.Ticket.GetDisplayName(_options.ServerSideSessions.UserDisplayNameClaimType),
                Created = item.Created,
                Renewed = item.Ticket.GetIssued(),
                Expires = item.Ticket.GetExpiration(),
                Issuer = item.Ticket.GetIssuer(),
                ClientIds = item.Ticket.Properties.GetClientList().ToList().AsReadOnly(),
                AuthenticationTicket = item.Ticket
            })
            .ToArray();
}
