// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.AccessTokenManagement.OpenIdConnect;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Duende.Bff;

/// <summary>
/// Default implementation of the ISessionRevocationService.
/// </summary>
public class SessionRevocationService : ISessionRevocationService
{
    private readonly BffOptions _options;
    private readonly IServerTicketStore _ticketStore;
    private readonly IUserSessionStore _sessionStore;
    private readonly IUserTokenEndpointService _tokenEndpoint;
    private readonly ILogger<SessionRevocationService> _logger;

    /// <summary>
    /// Ctor
    /// </summary>
    /// <param name="options"></param>
    /// <param name="ticketStore"></param>
    /// <param name="sessionStore"></param>
    /// <param name="tokenEndpoint"></param>
    /// <param name="logger"></param>
    public SessionRevocationService(IOptions<BffOptions> options, IServerTicketStore ticketStore, IUserSessionStore sessionStore, IUserTokenEndpointService tokenEndpoint, ILogger<SessionRevocationService> logger)
    {
        _options = options.Value;
        _ticketStore = ticketStore;
        _sessionStore = sessionStore;
        _tokenEndpoint = tokenEndpoint;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task RevokeSessionsAsync(UserSessionsFilter filter, CancellationToken cancellationToken = default)
    {
        if (_options.BackchannelLogoutAllUserSessions)
        {
            filter.SessionId = null;
        }

        _logger.LogDebug("Revoking sessions for sub {sub} and sid {sid}", filter.SubjectId, filter.SessionId);

        if (_options.RevokeRefreshTokenOnLogout)
        {
            var tickets = await _ticketStore.GetUserTicketsAsync(filter, cancellationToken);
            if (tickets?.Any() == true)
            {
                foreach (var ticket in tickets)
                {
                    var refreshToken = ticket.Properties.GetTokenValue("refresh_token");
                    if (!string.IsNullOrWhiteSpace(refreshToken))
                    {
                        await _tokenEndpoint.RevokeRefreshTokenAsync(new UserToken { RefreshToken = refreshToken }, new UserTokenRequestParameters(), cancellationToken);

                        _logger.LogDebug("Refresh token revoked for sub {sub} and sid {sid}", ticket.GetSubjectId(), ticket.GetSessionId());
                    }
                }
            }
        }

        await _sessionStore.DeleteUserSessionsAsync(filter);
    }
}
