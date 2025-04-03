// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.AccessTokenManagement.OpenIdConnect;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

// ReSharper disable once CheckNamespace
namespace Duende.Bff;

/// <summary>
/// Default implementation of the ISessionRevocationService.
/// </summary>
public class SessionRevocationService(
    IOptions<BffOptions> options,
    IServerTicketStore ticketStore,
    IUserSessionStore sessionStore,
    IUserTokenEndpointService tokenEndpoint,
    ILogger<SessionRevocationService> logger) : ISessionRevocationService
{
    private readonly BffOptions _options = options.Value;

    /// <inheritdoc/>
    public async Task RevokeSessionsAsync(UserSessionsFilter filter, CancellationToken cancellationToken = default)
    {
        if (_options.BackchannelLogoutAllUserSessions)
        {
            filter.SessionId = null;
        }

        logger.LogDebug("Revoking sessions for sub {sub} and sid {sid}", filter.SubjectId, filter.SessionId);

        if (_options.RevokeRefreshTokenOnLogout)
        {
            var tickets = await ticketStore.GetUserTicketsAsync(filter, cancellationToken);
            foreach (var ticket in tickets)
            {
                var refreshToken = ticket.Properties.GetTokenValue("refresh_token");
                if (!string.IsNullOrWhiteSpace(refreshToken))
                {
                    await tokenEndpoint.RevokeRefreshTokenAsync(new UserToken { RefreshToken = refreshToken }, new UserTokenRequestParameters(), cancellationToken);

                    logger.LogDebug("Refresh token revoked for sub {sub} and sid {sid}", ticket.GetSubjectId(), ticket.GetSessionId());
                }
            }
        }

        await sessionStore.DeleteUserSessionsAsync(filter, cancellationToken);
    }
}
