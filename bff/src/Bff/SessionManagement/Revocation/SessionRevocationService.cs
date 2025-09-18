// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.AccessTokenManagement;
using Duende.AccessTokenManagement.OpenIdConnect;
using Duende.Bff.Configuration;
using Duende.Bff.Otel;
using Duende.Bff.SessionManagement.SessionStore;
using Duende.Bff.SessionManagement.TicketStore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Duende.Bff.SessionManagement.Revocation;

/// <summary>
/// Default implementation of the ISessionRevocationService.
/// </summary>
internal class SessionRevocationService(
    IOptions<BffOptions> options,
    IServerTicketStore ticketStore,
    IUserSessionStore sessionStore,
    BuildUserSessionPartitionKey buildUserPartitionKey,
    IOpenIdConnectUserTokenEndpoint tokenEndpoint,
    ILogger<SessionRevocationService> logger) : ISessionRevocationService
{
    private readonly BffOptions _options = options.Value;

    /// <inheritdoc/>
    public async Task RevokeSessionsAsync(UserSessionsFilter filter, CT ct = default)
    {
        if (_options.BackchannelLogoutAllUserSessions)
        {
            filter.SessionId = null;
        }

        logger.RevokingSessions(LogLevel.Debug, filter.SubjectId, filter.SessionId);

        if (_options.RevokeRefreshTokenOnLogout)
        {
            var tickets = await ticketStore.GetUserTicketsAsync(filter, ct);
            foreach (var ticket in tickets)
            {
                var refreshToken = ticket.Properties.GetTokenValue("refresh_token");
                if (!string.IsNullOrWhiteSpace(refreshToken))
                {
                    await tokenEndpoint.RevokeRefreshTokenAsync(
                        new UserRefreshToken(RefreshToken.Parse(refreshToken),
                        options.Value.DPoPJsonWebKey), new UserTokenRequestParameters(), ct);

                    logger.RefreshTokenRevoked(LogLevel.Debug, ticket.GetSubjectId(), ticket.GetSessionId());
                }
            }
        }

        await sessionStore.DeleteUserSessionsAsync(buildUserPartitionKey(), filter, ct);
    }
}
