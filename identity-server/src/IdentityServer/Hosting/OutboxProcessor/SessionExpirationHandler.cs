// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Stores.Storage;
using Duende.IdentityServer.Stores.Storage.ServerSideSessions;
using Duende.Storage.Internal.Outbox;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Hosting.OutboxProcessor;

/// <summary>
/// Handles <see cref="OutboxEventName.EntityExpired"/> outbox events for server-side sessions
/// by deserializing the session, reconstructing the <see cref="UserSession"/>, and invoking
/// <see cref="ISessionCoordinationService.ProcessExpirationAsync"/> to trigger back-channel
/// logout notifications.
/// </summary>
internal sealed class SessionExpirationHandler(
    IDataProtectionProvider dataProtectionProvider,
    ISessionCoordinationService sessionCoordinationService,
    IdentityServerOptions options,
    TimeProvider timeProvider,
    ILogger<SessionExpirationHandler> logger) : IOutboxSubscriberHandler
{
    private readonly IDataProtector _protector =
        dataProtectionProvider.CreateProtector("Duende.SessionManagement.ServerSideTicketStore");

    /// <inheritdoc />
    public async Task<HandleOutcomeResult> HandleAsync(PersistedOutboxEvent item, Ct ct)
    {
        if (item.Dso is not ServerSideSessionDso.V1 dso)
        {
            return HandleOutcomeResult.Drop($"Expected ServerSideSessionDso.V1, got {item.Dso?.GetType().Name ?? "null"}");
        }

        ServerSideSession session;
        try
        {
            session = ServerSideSessionRepository.DsoToModel(dso);
        }
        catch (Exception ex)
        {
            logger.SessionHandlerPayloadDrop(LogLevel.Warning, ex, item.MessageId.Value, $"DSO to model conversion failed: {ex.Message}");
            return HandleOutcomeResult.Drop($"DSO to model conversion failed: {ex.Message}");
        }

        var ticket = session.Deserialize(_protector, logger);

        if (ticket is null)
        {
            logger.SessionHandlerPayloadDrop(LogLevel.Warning, item.MessageId.Value, "Ticket deserialization returned null");
            return HandleOutcomeResult.Drop("Ticket deserialization returned null");
        }

        UserSession userSession;
        try
        {
            userSession = new UserSession
            {
                SubjectId = ticket.GetSubjectId(),
                SessionId = ticket.GetSessionId(),
                DisplayName = ticket.GetDisplayName(options.ServerSideSessions.UserDisplayNameClaimType),
                Created = session.Created,
                Renewed = ticket.GetIssued(timeProvider),
                Expires = ticket.GetExpiration(),
                Issuer = ticket.GetIssuer(),
                ClientIds = ticket.Properties.GetClientList().ToList().AsReadOnly(),
                AuthenticationTicket = ticket
            };
        }
        catch (Exception ex)
        {
            logger.SessionHandlerPayloadDrop(LogLevel.Warning, ex, item.MessageId.Value, $"Failed to construct UserSession: {ex.Message}");
            return HandleOutcomeResult.Drop($"Failed to construct UserSession: {ex.Message}");
        }

        try
        {
            await sessionCoordinationService.ProcessExpirationAsync(userSession, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.SessionHandlerPayloadException(LogLevel.Warning, ex, item.MessageId.Value, ex.Message);
            return HandleOutcomeResult.Retry($"ProcessExpirationAsync failed: {ex.Message}");
        }

        return HandleOutcomeResult.Success();
    }
}
