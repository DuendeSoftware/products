// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using System.Security.Claims;
using System.Text.Json;
using Duende.IdentityModel;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Hosting.OutboxProcessor;
using Duende.IdentityServer.Stores.Storage;
using Duende.Storage;
using Duende.Storage.Internal;
using Duende.Storage.Internal.Outbox;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UnitTests.Common;

namespace UnitTests.Hosting.OutboxProcessor;

public class SessionExpirationHandlerTests
{
    private readonly IdentityServerOptions _options = new();
    private readonly ILogger<SessionExpirationHandler> _logger = TestLogger.Create<SessionExpirationHandler>();
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly StubSessionCoordinationService _coordinationService = new();

    public SessionExpirationHandlerTests()
    {
        var services = new ServiceCollection();
        services.AddDataProtection();
        var sp = services.BuildServiceProvider();
        _dataProtectionProvider = sp.GetRequiredService<IDataProtectionProvider>();
    }

    [Fact]
    public async Task non_server_side_session_dso_returns_drop()
    {
        var handler = CreateHandler();
        var dso = new ApiScopeDso.V1
        {
            Id = Guid.NewGuid(),
            Name = "Test Scope",
            Enabled = false,
            ShowInDiscoveryDocument = false,
            Required = false,
            Emphasize = false,
            UserClaims = [],
            ReferencedByApiResources = []
        };
        var evt = CreateEvent(dso);

        var result = await handler.HandleAsync(evt, CancellationToken.None);

        result.ShouldBeOfType<HandleOutcomeResult.DropResult>();
    }

    [Fact]
    public async Task null_payload_returns_drop()
    {
        var handler = CreateHandler();
        var evt = CreateEvent<ServerSideSessionDso.V1>(null);

        var result = await handler.HandleAsync(evt, CancellationToken.None);

        result.ShouldBeOfType<HandleOutcomeResult.DropResult>();
    }

    [Fact]
    public async Task valid_session_calls_coordination_service_and_returns_success()
    {
        var handler = CreateHandler();
        var payload = CreateValidSessionDso("sub_123", "sid_456");
        var evt = CreateEvent(payload);

        var result = await handler.HandleAsync(evt, CancellationToken.None);

        result.ShouldBeOfType<HandleOutcomeResult.SuccessResult>();
        _coordinationService.ProcessedSessions.Count.ShouldBe(1);
        _coordinationService.ProcessedSessions[0].SubjectId.ShouldBe("sub_123");
        _coordinationService.ProcessedSessions[0].SessionId.ShouldBe("sid_456");
    }

    [Fact]
    public async Task coordination_service_exception_returns_retry()
    {
        _coordinationService.ThrowOnProcess = new InvalidOperationException("Transient failure");
        var handler = CreateHandler();
        var dso = CreateValidSessionDso("sub_err", "sid_err");
        var evt = CreateEvent(dso);

        var result = await handler.HandleAsync(evt, CancellationToken.None);

        result.ShouldBeOfType<HandleOutcomeResult.RetryResult>();
        var retry = (HandleOutcomeResult.RetryResult)result;
        retry.Reason.ShouldContain("Transient failure");
    }

    [Fact]
    public async Task invalid_ticket_data_returns_drop()
    {
        var handler = CreateHandler();

        // Create a DSO with a garbage ticket that can't be deserialized
        var dso = new ServerSideSessionDso.V1
        {
            Key = "key_bad",
            Scheme = "idsrv",
            SubjectId = "sub_bad",
            SessionId = "sid_bad",
            CreatedUtcTicks = DateTime.UtcNow.Ticks,
            RenewedUtcTicks = DateTime.UtcNow.Ticks,
            ExpiresUtcTicks = DateTime.UtcNow.AddHours(1).Ticks,
            Ticket = "not-a-real-ticket"
        };
        var evt = CreateEvent(dso);

        var result = await handler.HandleAsync(evt, CancellationToken.None);

        result.ShouldBeOfType<HandleOutcomeResult.DropResult>();
    }

    private SessionExpirationHandler CreateHandler() =>
        new(
            _dataProtectionProvider,
            _coordinationService,
            _options,
            TimeProvider.System,
            _logger);

    private ServerSideSessionDso.V1 CreateValidSessionDso(string subjectId, string sessionId)
    {
        // Build an AuthenticationTicket with required claims
        var claims = new List<Claim>
        {
            new(JwtClaimTypes.Subject, subjectId)
        };
        var identity = new ClaimsIdentity(claims, "idsrv", JwtClaimTypes.Name, JwtClaimTypes.Role);
        var principal = new ClaimsPrincipal(identity);

        var props = new AuthenticationProperties();
        props.Items["session_id"] = sessionId;
        props.IssuedUtc = DateTimeOffset.UtcNow.AddMinutes(-30);
        props.ExpiresUtc = DateTimeOffset.UtcNow.AddHours(1);

        var ticket = new AuthenticationTicket(principal, props, "idsrv");

        // Serialize the ticket using the same protector purpose as the production code
        var protector = _dataProtectionProvider.CreateProtector("Duende.SessionManagement.ServerSideTicketStore");
        var serializedTicket = ticket.Serialize(protector);

        // Build the DSO
        var dso = new ServerSideSessionDso.V1
        {
            Key = $"key_{Guid.NewGuid():N}",
            Scheme = "idsrv",
            SubjectId = subjectId,
            SessionId = sessionId,
            CreatedUtcTicks = DateTime.UtcNow.Ticks,
            RenewedUtcTicks = DateTime.UtcNow.Ticks,
            ExpiresUtcTicks = DateTime.UtcNow.AddHours(1).Ticks,
            Ticket = serializedTicket
        };

        return dso;
    }

    private static PersistedOutboxEvent CreateEvent<T>(T? dso) where T : class, IDataStorageObject =>
        new()
        {
            MessageId = OutboxEventId.New(),
            EventId = OutboxEventId.New(),
            Timestamp = DateTimeOffset.UtcNow,
            SequenceNumber = 1,
            EventName = OutboxEventName.EntityExpired,
            SubjectId = UuidV7.New(),
            EntityTypeName = "ServerSideSessionDso",
            EntityTypeId = 2107,
            PoolId = 0,
            Payload = JsonSerializer.Serialize(dso),
            Dso = dso,
            SubscriberName = SubscriberName.Create("SessionExpiration")
        };
}
