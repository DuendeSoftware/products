// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Claims;
using System.Text.Json;
using Duende.IdentityModel;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Hosting.OutboxProcessor;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Stores;
using Duende.IdentityServer.Stores.Storage;
using Duende.IdentityServer.Stores.Storage.Clients;
using Duende.IdentityServer.Stores.Storage.PersistedGrants;
using Duende.Storage;
using Duende.Storage.Internal;
using Duende.Storage.Internal.Builder;
using Duende.Storage.Internal.Outbox;
using Duende.Storage.Internal.Querying.SearchFields;
using Duende.Storage.Schema;
using Duende.Storage.Sqlite;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using UnitTests.Common;
using UnitTests.Endpoints.EndSession;

// This file's own namespace is also named OutboxProcessor, which shadows the Storage type of the
// same name, so it is aliased here.
using StorageOutboxProcessor = Duende.Storage.Internal.Outbox.OutboxProcessor;

namespace UnitTests.Hosting.OutboxProcessor;

/// <summary>
/// End-to-end integration smoke test: writes an outbox event to the subscription queue via
/// the store's fanout mechanism, runs Storage's <see cref="StorageOutboxProcessor"/> the way
/// <see cref="OutboxProcessorHost"/> drives it, and verifies the event was consumed and the
/// coordination service was called.
/// </summary>
public class OutboxProcessorIntegrationTests
{
    private readonly IdentityServerOptions _options = new();

    [Fact]
    public async Task run_processor_processes_outbox_event_end_to_end()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataProtection();
        var dbName = $"processor_integration_{Guid.NewGuid():N}";
        var subscriptionName = SubscriberName.Create("SessionExpiration");
        var coordinationService = new StubSessionCoordinationService();

        // Register subscription in DI before building the store so OutboxSubscriptions picks it up
        services.AddSingleton<IOutboxSubscription>(new TestSubscription(subscriptionName));

        services.AddStorageInternal(storage =>
            storage.AddSqlite(opt =>
                opt.ConnectionString = $"Data Source={dbName};Mode=Memory;Cache=Shared"));

        services.AddDsoRegistration<ServerSideSessionDso.V1>();

        // The handler stack, registered in the same container the processor resolves scopes from.
        services.AddSingleton(_options);
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<ISessionCoordinationService>(coordinationService);
        services.AddKeyedTransient<IOutboxSubscriptionHandler, SessionExpirationHandler>(
            subscriptionName.Value);

        var sp = services.BuildServiceProvider();
        await sp.GetRequiredService<IStorageInstanceSchema>().MigrateAsync(Ct.None);

        var partitionedStorage = await sp.GetRequiredService<IPartitionedStorageFactory>().GetPartitionedStorageAsync(DataCategoryName.Operational, CancellationToken.None);
        var crossPartitionStorage = await sp.GetRequiredService<ICrossPartitionStorageFactory>()
            .GetCrossPartitionStorageAsync(StorageInstanceId.Default, CancellationToken.None);
        var dataProtectionProvider = sp.GetRequiredService<IDataProtectionProvider>();

        // Create a valid serialized session payload
        var payload = CreateValidSessionPayload(dataProtectionProvider, "sub_e2e", "sid_e2e");

        // Write an outbox event via CreateAsync so the store's fanout routes it to subscription queue.
        // We create a dummy entity and attach the outbox event to the same transaction.
        var entityId = UuidV7.New();
        var dso = new ServerSideSessionDso.V1
        {
            Key = $"dummy_{Guid.NewGuid():N}",
            Scheme = "idsrv",
            SubjectId = "sub_e2e",
            SessionId = "sid_e2e",
            CreatedUtcTicks = DateTime.UtcNow.Ticks,
            RenewedUtcTicks = DateTime.UtcNow.Ticks,
            ExpiresUtcTicks = DateTime.UtcNow.AddHours(1).Ticks,
            Ticket = "unused"
        };
        var outboxEvent = new OutboxEvent
        {
            Id = OutboxEventId.New(),
            Timestamp = DateTimeOffset.UtcNow,
            EventName = OutboxEventName.EntityExpired,
            SubjectId = entityId,
            EntityTypeName = "ServerSideSessionDso",
            EntityTypeId = (int)ServerSideSessionDso.EntityType.Id,
            DsoTypeSchemaVersion = 1,
            Payload = payload
        };
        await partitionedStorage.CreateAsync(
            entityId,
            dso,
            [],
            new SearchFieldCollection([]),
            Expiration.NoExpiration,
            [outboxEvent],
            CancellationToken.None);

        // Verify event exists before processor run
        var pageBefore = await crossPartitionStorage.GetOutboxEventsForSubscriptionAsync(subscriptionName, 100, CancellationToken.None);
        pageBefore.Events.Count.ShouldBeGreaterThan(0);

        // Act: drive the processor exactly as OutboxProcessorHost does.
        await sp.GetRequiredService<StorageOutboxProcessor>().RunProcessorAsync(CancellationToken.None);

        // Assert event consumed (re-query outbox returns empty)
        var pageAfter = await crossPartitionStorage.GetOutboxEventsForSubscriptionAsync(subscriptionName, 100, CancellationToken.None);
        pageAfter.Events.Count.ShouldBe(0);

        // Coordination service was called
        coordinationService.ProcessedSessions.Count.ShouldBe(1);
        coordinationService.ProcessedSessions[0].SubjectId.ShouldBe("sub_e2e");
        coordinationService.ProcessedSessions[0].SessionId.ShouldBe("sid_e2e");
    }

    private static string CreateValidSessionPayload(IDataProtectionProvider dataProtectionProvider, string subjectId, string sessionId)
    {
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
        var protector = dataProtectionProvider.CreateProtector("Duende.SessionManagement.ServerSideTicketStore");
        var serializedTicket = ticket.Serialize(protector);

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

        return JsonSerializer.Serialize(dso);
    }

    /// <summary>
    /// End-to-end session expiration test driven through the registered outbox processor.
    ///
    /// Unlike the smoke test above, which stubs the coordination service, this wires the real
    /// IdentityServer handler stack: ClientStore, PersistedGrantStore and
    /// DefaultSessionCoordinationService, all resolved from the same container that Duende.Storage
    /// registers the processor in. It asserts that expiring a session actually removes the
    /// persisted grants of a client that coordinates its lifetime with the user session.
    ///
    /// Everything here is resolved from registered infrastructure: the storage factory, the
    /// ambient outbox processing context and the OutboxProcessor itself all come from
    /// AddStorageInternal(), so the test exercises the same composition the product ships.
    /// </summary>
    [Fact]
    public async Task session_expiration_removes_grants_for_a_client_that_coordinates_lifetime()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataProtection();
        var dbName = $"session_expiration_{Guid.NewGuid():N}";
        var subscriptionName = SubscriberName.Create("SessionExpiration");

        services.AddSingleton<IOutboxSubscription>(new TestSubscription(subscriptionName));
        services.AddStorageInternal(storage =>
            storage.AddSqlite(opt =>
                opt.ConnectionString = $"Data Source={dbName};Mode=Memory;Cache=Shared"));
        services.AddDsoRegistration<ServerSideSessionDso.V1>();
        services.AddDsoRegistration<ClientDso.V1>();
        services.AddDsoRegistration<PersistedGrantDso.V1>();

        // The IdentityServer handler stack, registered the same way AddStorage() registers it so
        // the processor's own scope factory resolves it.
        services.AddSingleton(_options);
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<ClientRepository>();
        services.AddScoped<PersistedGrantRepository>();
        services.AddScoped<IClientStore, ClientStore>();
        services.AddScoped<IPersistedGrantStore, PersistedGrantStore>();
        services.AddSingleton<IBackChannelLogoutService>(new StubBackChannelLogoutClient());
        services.AddScoped<ISessionCoordinationService, DefaultSessionCoordinationService>();
        services.AddKeyedTransient<IOutboxSubscriptionHandler, SessionExpirationHandler>(
            subscriptionName.Value);

        var sp = services.BuildServiceProvider();
        await sp.GetRequiredService<IStorageInstanceSchema>().MigrateAsync(Ct.None);

        var partitionedStorage = await sp.GetRequiredService<IPartitionedStorageFactory>().GetPartitionedStorageAsync(DataCategoryName.Operational, Ct.None);
        var crossPartitionStorage = await sp.GetRequiredService<ICrossPartitionStorageFactory>()
            .GetCrossPartitionStorageAsync(StorageInstanceId.Default, Ct.None);

        const string clientId = "coordinated_client";
        const string subjectId = "sub_expiring";
        const string sessionId = "sid_expiring";
        const string grantKey = "grant_expiring";

        var clientDso = BuildMinimalClientDso(clientId, coordinateLifetime: true);
        await partitionedStorage.CreateAsync(
            UuidV7.New(),
            clientDso,
            [DataStorageKey.Create(ClientIdDskV1.Create(clientId))],
            BuildClientSearchFields(clientDso),
            Expiration.NoExpiration,
            [],
            Ct.None);

        var grantId = UuidV7.New();
        var grantDso = new PersistedGrantDso.V1
        {
            Id = grantId.Value,
            Key = grantKey,
            Type = "refresh_token",
            SubjectId = subjectId,
            SessionId = sessionId,
            ClientId = clientId,
            CreationTimeTicks = DateTime.UtcNow.Ticks,
            ExpirationTicks = DateTime.UtcNow.AddHours(1).Ticks,
            Data = "{}"
        };
        await partitionedStorage.CreateAsync(
            grantId,
            grantDso,
            [DataStorageKey.Create(PersistedGrantKeyDskV1.Create(grantKey))],
            BuildGrantSearchFields(grantDso),
            Expiration.NoExpiration,
            [],
            Ct.None);

        var dataProtectionProvider = sp.GetRequiredService<IDataProtectionProvider>();
        var sessionPayload = CreateValidSessionPayloadWithClients(
            dataProtectionProvider, subjectId, sessionId, [clientId]);

        var sessionEntityId = UuidV7.New();
        var sessionDso = new ServerSideSessionDso.V1
        {
            Key = $"sess_{Guid.NewGuid():N}",
            Scheme = "idsrv",
            SubjectId = subjectId,
            SessionId = sessionId,
            CreatedUtcTicks = DateTime.UtcNow.Ticks,
            RenewedUtcTicks = DateTime.UtcNow.Ticks,
            ExpiresUtcTicks = DateTime.UtcNow.AddHours(1).Ticks,
            Ticket = "unused"
        };
        var outboxEvent = new OutboxEvent
        {
            Id = OutboxEventId.New(),
            Timestamp = DateTimeOffset.UtcNow,
            EventName = OutboxEventName.EntityExpired,
            SubjectId = sessionEntityId,
            EntityTypeName = "ServerSideSessionDso",
            EntityTypeId = (int)ServerSideSessionDso.EntityType.Id,
            DsoTypeSchemaVersion = 1,
            Payload = sessionPayload
        };
        await partitionedStorage.CreateAsync(
            sessionEntityId,
            sessionDso,
            [],
            new SearchFieldCollection([]),
            Expiration.NoExpiration,
            [outboxEvent],
            Ct.None);

        var queuedBefore = await crossPartitionStorage.GetOutboxEventsForSubscriptionAsync(subscriptionName, 100, Ct.None);
        queuedBefore.Events.Count.ShouldBe(1);

        // Act: drive the processor exactly as OutboxProcessorHost does.
        await sp.GetRequiredService<StorageOutboxProcessor>().RunProcessorAsync(Ct.None);

        var queuedAfter = await crossPartitionStorage.GetOutboxEventsForSubscriptionAsync(subscriptionName, 100, Ct.None);
        queuedAfter.Events.Count.ShouldBe(0, "event was consumed by the processor");

        // The grant should be removed because:
        //   - SessionExpirationHandler called DefaultSessionCoordinationService.ProcessExpirationAsync
        //   - ProcessExpirationAsync found the client
        //   - The client coordinates lifetime with user sessions
        //   - PersistedGrantStore.RemoveAllAsync removed the grant
        var grantResult = await partitionedStorage.TryReadAsync(
            PersistedGrantDso.EntityType,
            DataStorageKey.Create(PersistedGrantKeyDskV1.Create(grantKey)),
            Ct.None);
        grantResult.Found.ShouldBeFalse(
            "the persisted grant should be removed when processing the session expiration event");
    }

    private static string CreateValidSessionPayloadWithClients(
        IDataProtectionProvider dataProtectionProvider,
        string subjectId,
        string sessionId,
        IEnumerable<string> clientIds)
    {
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

        // Embed the client list the same way IdentityServer does.
        foreach (var clientId in clientIds)
        {
            props.SetClientList(props.GetClientList().Append(clientId));
        }

        var ticket = new AuthenticationTicket(principal, props, "idsrv");
        var protector = dataProtectionProvider.CreateProtector("Duende.SessionManagement.ServerSideTicketStore");
        var serializedTicket = ticket.Serialize(protector);

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

        return JsonSerializer.Serialize(dso);
    }

    private static ClientDso.V1 BuildMinimalClientDso(string clientId, bool coordinateLifetime) => new()
    {
        Id = UuidV7.New().Value,
        ClientId = clientId,
        Enabled = true,
        ProtocolType = "oidc",
        RequireClientSecret = false,
        RequirePkce = false,
        AllowPlainTextPkce = false,
        RequireRequestObject = false,
        RequireDPoP = false,
        DPoPValidationMode = 0,
        DPoPClockSkewTicks = TimeSpan.FromMinutes(5).Ticks,
        RequireConsent = false,
        AllowRememberConsent = true,
        AllowAccessTokensViaBrowser = false,
        AllowOfflineAccess = true,
        AccessTokenType = 0,
        IncludeJwtId = false,
        IdentityTokenLifetime = 300,
        AccessTokenLifetime = 3600,
        AuthorizationCodeLifetime = 300,
        AbsoluteRefreshTokenLifetime = 2592000,
        SlidingRefreshTokenLifetime = 1296000,
        RefreshTokenUsage = 1,
        RefreshTokenExpiration = 1,
        UpdateAccessTokenClaimsOnRefresh = false,
        AlwaysIncludeUserClaimsInIdToken = false,
        AlwaysSendClientClaims = false,
        EnableLocalLogin = true,
        FrontChannelLogoutSessionRequired = true,
        BackChannelLogoutSessionRequired = true,
        RequirePushedAuthorization = false,
        DeviceCodeLifetime = 300,
        CoordinateLifetimeWithUserSession = coordinateLifetime,
        AllowedGrantTypes = ["authorization_code", "refresh_token"],
        AllowedScopes = ["openid", "offline_access"],
        RedirectUris = [],
        PostLogoutRedirectUris = [],
        AllowedIdentityTokenSigningAlgorithms = [],
        IdentityProviderRestrictions = [],
        AllowedCorsOrigins = [],
        ClientSecrets = [],
        Claims = []
    };

    private static SearchFieldCollection BuildClientSearchFields(ClientDso.V1 dso)
    {
        var builder = new SearchFieldsBuilder()
            .Add("ClientId", dso.ClientId)
            .Add("Enabled", dso.Enabled);

        var i = 0;
        foreach (var gt in dso.AllowedGrantTypes)
        {
            _ = builder.Add("GrantType", i++, gt);
        }

        return builder.Build();
    }

    private static SearchFieldCollection BuildGrantSearchFields(PersistedGrantDso.V1 dso)
    {
        var builder = new SearchFieldsBuilder()
            .Add("ClientId", dso.ClientId)
            .Add("Type", dso.Type);

        if (dso.SubjectId is not null)
        {
            _ = builder.Add("SubjectId", dso.SubjectId);
        }

        if (dso.SessionId is not null)
        {
            _ = builder.Add("SessionId", dso.SessionId);
        }

        return builder.Build();
    }

    private sealed class TestSubscription(SubscriberName name) : IOutboxSubscription
    {
        public SubscriberName SubscriberName => name;
        public bool IsEnabled => true;
        public IReadOnlySet<OutboxEventName> EventNames { get; } =
            new HashSet<OutboxEventName> { OutboxEventName.EntityExpired };
        public IReadOnlySet<int> EntityTypeIds { get; } = new HashSet<int> { 2107 };
    }
}

