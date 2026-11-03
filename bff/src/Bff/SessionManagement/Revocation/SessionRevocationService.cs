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
    public async Task RevokeSessionsAsync(UserSessionsFilter filter, Ct ct = default)
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
                    await TryRevokeRefreshTokenAsync(ticket, refreshToken, filter, ct);
                }
            }
        }

        await sessionStore.DeleteUserSessionsAsync(buildUserPartitionKey(), filter, ct);
    }

    /// <summary>
    /// Revokes a single session's refresh token. Revocation is best effort: a failure is logged
    /// and swallowed, so that it can't stop the remaining tokens from being revoked or the
    /// user's sessions from being deleted.
    /// </summary>
    private async Task TryRevokeRefreshTokenAsync(AuthenticationTicket ticket, string refreshToken, UserSessionsFilter filter, Ct ct)
    {
        try
        {
            await tokenEndpoint.RevokeRefreshTokenAsync(
                // This refresh token comes from our own session store and is sent back to the provider
                // that issued it, so refusing an oversized token here would mean it never gets revoked.
                new UserRefreshToken(RefreshToken.Parse(refreshToken, int.MaxValue), _options.DPoPJsonWebKey),
                new UserTokenRequestParameters(),
                ct);

            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.RefreshTokenRevoked(LogLevel.Debug, ticket.GetSubjectId(), ticket.GetSessionId());
            }
        }
        catch (Exception ex) when (!(ex is OperationCanceledException && ct.IsCancellationRequested))
        {
            logger.FailedToRevokeRefreshToken(LogLevel.Warning, ex, filter.SubjectId, ticket.GetSessionId());
        }
    }
}
