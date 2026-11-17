// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Globalization;
using System.Security.Cryptography;
using System.Text;
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
    private const string SessionVersionItem = "idsrv.server_session_version";
    private const string SessionFingerprintItem = "idsrv.server_session_fingerprint";
    private const string IssuedItem = ".issued";
    private const string ExpiresItem = ".expires";

    private readonly IdentityServerOptions _options;
    private readonly IIssuerNameService _issuerNameService;
    private readonly IServerSideSessionStore _store;
    private readonly ISessionReplacementService _sessionReplacementService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IDataProtector _protector;
    private readonly ILogger<ServerSideTicketStore> _logger;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// ctor
    /// </summary>
    /// <param name="options"></param>
    /// <param name="issuerNameService"></param>
    /// <param name="store"></param>
    /// <param name="sessionReplacementService"></param>
    /// <param name="dataProtectionProvider"></param>
    /// <param name="httpContextAccessor"></param>
    /// <param name="logger"></param>
    /// <param name="timeProvider"></param>
    public ServerSideTicketStore(
        IdentityServerOptions options,
        IIssuerNameService issuerNameService,
        IServerSideSessionStore store,
        ISessionReplacementService sessionReplacementService,
        IDataProtectionProvider dataProtectionProvider,
        IHttpContextAccessor httpContextAccessor,
        ILogger<ServerSideTicketStore> logger,
        TimeProvider timeProvider)
    {
        _options = options;
        _issuerNameService = issuerNameService;
        _store = store;
        _sessionReplacementService = sessionReplacementService;
        _httpContextAccessor = httpContextAccessor;
        _protector = dataProtectionProvider.CreateProtector("Duende.SessionManagement.ServerSideTicketStore");
        _logger = logger;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc />
    public async Task<string> StoreAsync(AuthenticationTicket ticket)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("ServerSideTicketStore.Store");

        ArgumentNullException.ThrowIfNull(ticket);

        ticket.SetIssuer(await _issuerNameService.GetCurrentAsync(_httpContextAccessor.HttpContext?.RequestAborted ?? Ct.None));

        var key = CryptoRandom.CreateUniqueId(format: CryptoRandom.OutputFormat.Hex);

        await CreateNewSessionAsync(key, ticket);

        return key;
    }

    private async Task CreateNewSessionAsync(string key, AuthenticationTicket ticket)
    {
        _logger.CreatingEntryInStoreForAuthenticationTicketKeyKey(key, ticket.GetExpiration());

        ticket.Properties.Items.Remove(SessionVersionItem);
        ticket.Properties.Items.Remove(SessionFingerprintItem);

        var session = new ServerSideSession
        {
            Key = key,
            Scheme = ticket.AuthenticationScheme,
            Created = ticket.GetIssued(_timeProvider),
            Renewed = ticket.GetIssued(_timeProvider),
            Expires = ticket.GetExpiration(),
            SubjectId = ticket.GetSubjectId(),
            SessionId = ticket.GetSessionId(),
            DisplayName = ticket.GetDisplayName(_options.ServerSideSessions.UserDisplayNameClaimType),
            Ticket = ticket.Serialize(_protector)
        };

        await _store.CreateSessionAsync(session, _httpContextAccessor.HttpContext?.RequestAborted ?? Ct.None);
    }

    /// <inheritdoc />
    public async Task<AuthenticationTicket> RetrieveAsync(string key)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("ServerSideTicketStore.Retrieve");

        ArgumentNullException.ThrowIfNull(key);

        _logger.RetrieveAuthenticationTicketForKeyKey(key);

        var session = await _store.GetSessionAsync(key, _httpContextAccessor.HttpContext?.RequestAborted ?? Ct.None);
        if (session == null)
        {
            _logger.NoTicketFoundInStoreForKey(key);
            return null;
        }

        var ticket = session.Deserialize(_protector, _logger);
        if (ticket != null)
        {
            _logger.TicketLoadedForKeyKeyWithExpirationExpiration(key, ticket.GetExpiration());
            ticket.Properties.Items[SessionVersionItem] = session.Version.ToString(CultureInfo.InvariantCulture);
            ticket.Properties.Items[SessionFingerprintItem] = ComputeFingerprint(ticket);
            return ticket;
        }

        // if we failed to get a ticket, then remove DB record
        _logger.FailedToDeserializeAuthenticationTicketFromStoreDeleting(key);
        await RemoveAsync(key);

        return null;
    }

    /// <inheritdoc />
    public async Task RenewAsync(string key, AuthenticationTicket ticket)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("ServerSideTicketStore.Renew");

        ArgumentNullException.ThrowIfNull(ticket);

        var versionRead = ticket.Properties.Items.Remove(SessionVersionItem, out var versionText)
            ? int.Parse(versionText, CultureInfo.InvariantCulture)
            : 0;
        var fingerprintRead = ticket.Properties.Items.Remove(SessionFingerprintItem, out var fingerprintText) ? fingerprintText : null;
        var fingerprintToSave = ComputeFingerprint(ticket);

        var session = await _store.GetSessionAsync(key, _httpContextAccessor.HttpContext?.RequestAborted ?? Ct.None);
        if (session == null)
        {
            return;
        }

        session.Version = versionRead;

        _logger.RenewingAuthenticationTicketForKeyKeyWithExpirationExpiration(key, ticket.GetExpiration());

        var sub = ticket.GetSubjectId();
        var sid = ticket.GetSessionId();
        var name = string.IsNullOrWhiteSpace(_options.ServerSideSessions.UserDisplayNameClaimType) ? null : ticket.Principal.FindFirst(_options.ServerSideSessions.UserDisplayNameClaimType)?.Value;

        var isNew = session.SubjectId != sub || session.SessionId != sid;
        if (isNew)
        {
            _logger.SessionReplacementDetectedForKey(key, session.SubjectId, session.SessionId);

            var previousSession = AsUserSessions([session]).SingleOrDefault();
            if (previousSession == null)
            {
                _logger.CouldNotReadPreviousSessionTicketForKey(key, session.SubjectId, session.SessionId);
            }

            await _sessionReplacementService.ProcessReplacementAsync(new SessionReplacementContext
            {
                PreviousSubjectId = session.SubjectId,
                PreviousSessionId = session.SessionId,
                PreviousSession = previousSession,
                NewSubjectId = sub,
                NewSessionId = sid
            }, _httpContextAccessor.HttpContext?.RequestAborted ?? Ct.None);

            if (session.SubjectId != sub)
            {
                await _store.DeleteSessionAsync(key, _httpContextAccessor.HttpContext?.RequestAborted ?? Ct.None);
                return;
            }

            session.Created = ticket.GetIssued(_timeProvider);
            session.SessionId = sid;
        }

        if (ticket.GetIssuer() == null)
        {
            // when issuing a new cookie on top of an existing cookie, the AuthenticationTicket passed above is new (and not the prior one loaded from the ticket store)
            ticket.SetIssuer(await _issuerNameService.GetCurrentAsync(_httpContextAccessor.HttpContext?.RequestAborted ?? Ct.None));
        }
        session.Renewed = ticket.GetIssued(_timeProvider);
        session.Expires = ticket.GetExpiration();
        session.DisplayName = name;
        session.Ticket = ticket.Serialize(_protector);

        if (!await _store.UpdateSessionAsync(session, _httpContextAccessor.HttpContext?.RequestAborted ?? Ct.None))
        {
            if (fingerprintRead == fingerprintToSave)
            {
                _logger.SkippingRenewalOfTicketForKeyBecauseOnlyDatesDiffer(key);
                return;
            }

            throw new ServerSideSessionConflictException();
        }
    }

    /// <summary>
    /// Hashes everything the store saves from a ticket except when it was issued and when it expires, so that two tickets
    /// with the same fingerprint differ only in those dates. The lifetime between the two dates is included: a cookie
    /// renewal keeps it, so it still matches, while a save that shortens or lengthens the session does not.
    /// The claims come from the same lite form that is serialized, and claims and items are sorted so that the order
    /// they were added in does not change the result.
    /// </summary>
    private static string ComputeFingerprint(AuthenticationTicket ticket)
    {
        var canonical = new StringBuilder();

        var lifetime = ticket.Properties.ExpiresUtc - ticket.Properties.IssuedUtc;
        Append(canonical, "scheme", ticket.AuthenticationScheme);
        Append(canonical, "lifetime", lifetime?.Ticks.ToString(CultureInfo.InvariantCulture));

        var principal = ticket.Principal.ToClaimsPrincipalLite();
        Append(canonical, "authentication-type", principal.AuthenticationType);

        foreach (var claim in principal.Claims
                     .OrderBy(c => c.Type, StringComparer.Ordinal)
                     .ThenBy(c => c.Value, StringComparer.Ordinal)
                     .ThenBy(c => c.ValueType, StringComparer.Ordinal)
                     .ThenBy(c => c.Issuer, StringComparer.Ordinal))
        {
            Append(canonical, "claim", claim.Type, claim.Value, claim.ValueType, claim.Issuer);
        }

        foreach (var item in ticket.Properties.Items.OrderBy(i => i.Key, StringComparer.Ordinal))
        {
            if (item.Key is IssuedItem or ExpiresItem or SessionVersionItem or SessionFingerprintItem or IdentityServerConstants.ForceCookieRenewalFlag)
            {
                continue;
            }

            Append(canonical, "item", item.Key, item.Value);
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())));

        static void Append(StringBuilder builder, params string[] parts)
        {
            foreach (var part in parts)
            {
                builder.Append(part?.Length ?? -1).Append(':').Append(part);
            }

            builder.Append('\n');
        }
    }

    /// <inheritdoc />
    public async Task RemoveAsync(string key)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("ServerSideTicketStore.Remove");

        ArgumentNullException.ThrowIfNull(key);

        _logger.RemovingAuthenticationTicketFromStoreForKeyKey(key);

        // There is a somewhat rare scenario where a session has expired and a request to IdentityServer happens prior
        // to the cleanup job running. When that happens, the session is removed but none of the processing to trigger
        // backchannel logouts, etc. happens so we need a way to kick that off and are doing so here.
        var session = await _store.GetSessionAsync(key, _httpContextAccessor.HttpContext?.RequestAborted ?? Ct.None);
        if (session != null)
        {
            var userSession = AsUserSessions([session]).SingleOrDefault();
            if (userSession != null)
            {
                _httpContextAccessor.HttpContext?.SetExpiredUserSession(userSession);
            }
        }

        await _store.DeleteSessionAsync(key, _httpContextAccessor.HttpContext?.RequestAborted ?? Ct.None);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<UserSession>> GetSessionsAsync(SessionFilter filter, Ct ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("ServerSideTicketStore.GetSessions");

        var sessions = await _store.GetSessionsAsync(filter, ct);

        return AsUserSessions(sessions);

    }

    /// <inheritdoc />
    public async Task<QueryResult<UserSession>> QuerySessionsAsync(SessionQuery filter, Ct ct)
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
    public async Task<IReadOnlyCollection<UserSession>> GetAndRemoveExpiredSessionsAsync(int count, Ct ct)
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
                Renewed = item.Ticket.GetIssued(_timeProvider),
                Expires = item.Ticket.GetExpiration(),
                Issuer = item.Ticket.GetIssuer(),
                ClientIds = item.Ticket.Properties.GetClientList().ToList().AsReadOnly(),
                AuthenticationTicket = item.Ticket
            })
            .ToArray();
}
