// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Stores;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Services;

/// <summary>
/// Default session replacement service. Revokes the previous session's token grants, but only
/// for clients whose lifetime is coordinated with the user session.
/// The candidate clients are the previous session's client list (when its ticket could be read)
/// plus the clients that hold persisted token grants (refresh tokens, reference tokens,
/// authorization codes, and backchannel authentication requests) for the previous session's
/// subject and session id. This is because some flows, such as CIBA, can issue grants tied to a
/// session without adding the client to the session's client list. A client that can no longer be
/// found is treated as coordinated, so its grants are revoked too.
/// </summary>
public sealed class DefaultSessionReplacementService : ISessionReplacementService
{
    private readonly IdentityServerOptions _options;
    private readonly IClientStore _clientStore;
    private readonly IPersistedGrantStore _persistedGrantStore;
    private readonly ILogger<DefaultSessionReplacementService> _logger;

    private static readonly string[] TokenTypes =
    [
        IdentityServerConstants.PersistedGrantTypes.RefreshToken,
        IdentityServerConstants.PersistedGrantTypes.ReferenceToken,
        IdentityServerConstants.PersistedGrantTypes.AuthorizationCode,
        IdentityServerConstants.PersistedGrantTypes.BackChannelAuthenticationRequest,
    ];

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultSessionReplacementService"/> class.
    /// </summary>
    /// <param name="options">The IdentityServer options.</param>
    /// <param name="clientStore">The client store.</param>
    /// <param name="persistedGrantStore">The persisted grant store.</param>
    /// <param name="logger">The logger.</param>
    public DefaultSessionReplacementService(
        IdentityServerOptions options,
        IClientStore clientStore,
        IPersistedGrantStore persistedGrantStore,
        ILogger<DefaultSessionReplacementService> logger)
    {
        _options = options;
        _clientStore = clientStore;
        _persistedGrantStore = persistedGrantStore;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task ProcessReplacementAsync(SessionReplacementContext context, Ct ct)
    {
        using var activity = Tracing.ServiceActivitySource.StartActivity("DefaultSessionReplacementService.ProcessReplacement");

        ArgumentNullException.ThrowIfNull(context);

        var previousSubjectId = context.PreviousSubjectId;
        var previousSessionId = context.PreviousSessionId;
        // Grant stores ignore blank subject and session filters, so revoking without both
        // would reach grants of other sessions or users.
        if (string.IsNullOrWhiteSpace(previousSubjectId) || string.IsNullOrWhiteSpace(previousSessionId))
        {
            return;
        }

        var coordinated = new List<string>();

        var candidateClientIds = (context.PreviousSession?.ClientIds ?? []).ToList();

        var grants = await _persistedGrantStore.GetAllAsync(new PersistedGrantFilter
        {
            SubjectId = previousSubjectId,
            SessionId = previousSessionId,
            Types = TokenTypes
        }, ct);

        foreach (var grant in grants)
        {
            if (!string.IsNullOrEmpty(grant.ClientId))
            {
                candidateClientIds.Add(grant.ClientId);
            }
        }

        foreach (var clientId in candidateClientIds.Distinct(StringComparer.Ordinal))
        {
            // disabled clients still count: their grants belong to the previous session
            var client = await _clientStore.FindClientByIdAsync(clientId, ct);

            // A replacement sends no notifications, so revoking grants of a client that no
            // longer exists is harmless and fails closed: a client recreated later with the
            // same id must not pick up the old grants.
            if (client == null || client.ShouldCoordinateLifetimeWithUserSession(_options))
            {
                coordinated.Add(clientId);
            }
        }

        if (coordinated.Count == 0)
        {
            return;
        }

        _logger.DueToSessionReplacementRemovingTokensForSubject(previousSubjectId, previousSessionId, string.Join(", ", coordinated));

        // intentionally no back-channel logout here: a session replacement is not a logout
        await _persistedGrantStore.RemoveAllAsync(new PersistedGrantFilter
        {
            SubjectId = previousSubjectId,
            SessionId = previousSessionId,
            ClientIds = coordinated,
            Types = TokenTypes
        }, ct);
    }
}
