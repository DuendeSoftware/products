// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Claims;
using Duende.AccessTokenManagement;
using Duende.AccessTokenManagement.OpenIdConnect;
using Duende.Bff.Otel;
using Duende.Bff.SessionManagement.SessionStore;
using Duende.Bff.SessionManagement.TicketStore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;

namespace Duende.Bff.Blazor;

/// <summary>
/// A token store that retrieves tokens from server side sessions.
/// </summary>
internal class ServerSideTokenStore(
    IStoreTokensInAuthenticationProperties tokensInAuthProperties,
    IUserSessionStore sessionStore,
    IDataProtectionProvider dataProtectionProvider,
    ILogger<ServerSideTokenStore> logger,
    AuthenticationStateProvider authenticationStateProvider) : IUserTokenStore
{
    private readonly IDataProtector _protector =
        dataProtectionProvider.CreateProtector(ServerSideTicketStore.DataProtectorPurpose);

    private readonly IHostEnvironmentAuthenticationStateProvider _authenticationStateProvider = authenticationStateProvider as IHostEnvironmentAuthenticationStateProvider
        ?? throw new ArgumentException("AuthenticationStateProvider must implement IHostEnvironmentAuthenticationStateProvider");

    public async Task<TokenResult<TokenForParameters>> GetTokenAsync(ClaimsPrincipal user, UserTokenRequestParameters? parameters = null,
        CancellationToken ct = default)
    {
        logger.RetrievingTokenForUser(LogLevel.Debug, user.Identity?.Name);
        var session = await GetSession(user);
        if (session == null)
        {
            var anonymous = new ClaimsPrincipal(new ClaimsIdentity());
            var loggedOutTask = Task.FromResult(new AuthenticationState(user: anonymous));
            _authenticationStateProvider.SetAuthenticationState(loggedOutTask);
            return new FailedResult("Session not found");
        }

        var ticket = session.Deserialize(_protector, logger) ??
                     throw new InvalidOperationException("Failed to deserialize authentication ticket from session");

        return tokensInAuthProperties.GetUserToken(ticket.Properties, parameters);
    }

    private async Task<UserSession?> GetSession(ClaimsPrincipal user)
    {
        if (user.Identity?.IsAuthenticated == false)
        {
            return null;
        }
        var sub = user.FindFirst("sub")?.Value ?? throw new InvalidOperationException("no sub claim");
        var sid = user.FindFirst("sid")?.Value ?? throw new InvalidOperationException("no sid claim");

        logger.RetrievingSession(LogLevel.Debug, sid, sub);

        var sessions = await sessionStore.GetUserSessionsAsync(new UserSessionsFilter
        {
            SubjectId = sub,
            SessionId = sid
        });

        if (sessions.Count == 0)
        {
            return null;
        }

        if (sessions.Count > 1)
        {
            throw new InvalidOperationException("Multiple tickets found");
        }

        return sessions.First();
    }

    public async Task StoreTokenAsync(ClaimsPrincipal user, UserToken token,
        UserTokenRequestParameters? parameters = null, CT ct = default)
    {
        logger.StoringTokenForUser(LogLevel.Debug, user.Identity?.Name);
        await UpdateTicket(user,
            async ticket => { await tokensInAuthProperties.SetUserTokenAsync(token, ticket.Properties, parameters, ct); });
    }


    public async Task ClearTokenAsync(ClaimsPrincipal user, UserTokenRequestParameters? parameters = null, CT ct = default)
    {
        logger.RemovingTokenForUser(LogLevel.Debug, user.Identity?.Name);
        await UpdateTicket(user, ticket =>
        {
            tokensInAuthProperties.RemoveUserToken(ticket.Properties, parameters);
            return Task.CompletedTask;
        });
    }

    protected async Task UpdateTicket(ClaimsPrincipal user, Func<AuthenticationTicket, Task> updateAction)
    {
        var session = await GetSession(user);
        if (session == null)
        {
            logger.FailedToFindSessionToUpdate(LogLevel.Debug);
            return;
        }

        var ticket = session.Deserialize(_protector, logger) ??
                     throw new InvalidOperationException("Failed to deserialize authentication ticket from session");

        await updateAction(ticket);

        session.Ticket = ticket.Serialize(_protector);

        await sessionStore.UpdateUserSessionAsync(session.Key, session);
    }
}
