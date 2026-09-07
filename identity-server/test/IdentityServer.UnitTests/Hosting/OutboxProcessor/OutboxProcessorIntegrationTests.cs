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
using Duende.Storage.Sqlite;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UnitTests.Common;
using UnitTests.Endpoints.EndSession;

namespace UnitTests.Hosting.OutboxProcessor;

/// <summary>
/// End-to-end integration smoke test: writes an outbox event to the subscriber queue via
/// the store's fanout mechanism, runs <see cref="OutboxProcessorHost.RunProcessorAsync"/>, and verifies
/// the event was consumed and the coordination service was called.
/// </summary>
public class OutboxProcessorIntegrationTests
{
    private readonly IdentityServerOptions _options = new();
    private readonly ILogger<OutboxProcessorHost> _hostLogger = TestLogger.Create<OutboxProcessorHost>();

    [Fact]
    public async Task run_processor_processes_outbox_event_end_to_end()
    {
        // Arrange build SQLite store with outbox subscriber registered in DI
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataProtection();
        var dbName = $"processor_integration_{Guid.NewGuid():N}";
        var subscriberName = SubscriberName.Create("SessionExpiration");

        var subscriber = new TestSubscriber(subscriberName);
        var coordinationService = new StubSessionCoordinationService();

        // Register subscriber in DI before building the store so OutboxSubscribers picks it up
        services.AddSingleton<IOutboxSubscriber>(subscriber);

        services.AddStorageInternal(storage =>
            storage.AddSqliteStore(opt =>
                opt.ConnectionString = $"Data Source={dbName};Mode=Memory;Cache=Shared"));

        services.AddDsoRegistration<ServerSideSessionDso.V1>();

        var sp = services.BuildServiceProvider();
        var pooledStore = sp.GetRequiredService<IPooledStore>();
        await ((Duende.Storage.Schema.IDatabaseSchema)pooledStore).MigrateAsync(CancellationToken.None);

        var storage = pooledStore.OpenPool(0);
        var storageFactory = new SimpleStorageFactory(storage);
        var dataProtectionProvider = sp.GetRequiredService<IDataProtectionProvider>();

        // Create a valid serialized session payload
        var payload = CreateValidSessionPayload(dataProtectionProvider, "sub_e2e", "sid_e2e");

        // Write an outbox event via CreateAsync so the store's fanout routes it to subscriber queue.
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
        await storage.CreateAsync(
            entityId,
            dso,
            [],
            new SearchFieldCollection([]),
            Expiration.NoExpiration,
            [outboxEvent],
            CancellationToken.None);

        // Verify event exists before processor run
        var pageBefore = await storage.GetOutboxEventsForSubscriberAsync(subscriberName, 100, CancellationToken.None);
        pageBefore.Events.Count.ShouldBeGreaterThan(0);

        // Build a second service provider for the handler scope, reusing the same
        // IDataProtectionProvider so ticket encryption/decryption uses the same key ring.
        var handlerServices = new ServiceCollection();
        handlerServices.AddLogging();
        handlerServices.AddSingleton(dataProtectionProvider);
        handlerServices.AddSingleton(_options);
        handlerServices.AddSingleton(TimeProvider.System);
        handlerServices.AddSingleton<ISessionCoordinationService>(coordinationService);
        handlerServices.AddKeyedTransient<IOutboxSubscriberHandler>(
            subscriberName.Value,
            (svcProvider, _) => new SessionExpirationHandler(
                svcProvider.GetRequiredService<IDataProtectionProvider>(),
                svcProvider.GetRequiredService<ISessionCoordinationService>(),
                svcProvider.GetRequiredService<IdentityServerOptions>(),
                svcProvider.GetRequiredService<TimeProvider>(),
                TestLogger.Create<SessionExpirationHandler>()));

        var handlerSp = handlerServices.BuildServiceProvider();
        var scopeFactory = handlerSp.GetRequiredService<IServiceScopeFactory>();

        // Act run processor
        IEnumerable<IOutboxSubscriber> subscribers = [subscriber];
        var host = new OutboxProcessorHost(
            storageFactory,
            subscribers,
            scopeFactory,
            _options,
            TimeProvider.System,
            _hostLogger);

        await host.RunProcessorAsync(CancellationToken.None);

        // Assert event consumed (re-query outbox returns empty)
        var pageAfter = await storage.GetOutboxEventsForSubscriberAsync(subscriberName, 100, CancellationToken.None);
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
    /// End-to-end pool-isolation regression test.
    ///
    /// Scenario: a tenant's session and its persisted grants live in pool 1. A
    /// client with CoordinateLifetimeWithUserSession=true also lives only in pool 1.
    /// The outbox event that records the session expiration is therefore also
    /// written in pool 1.
    ///
    /// Without pool-aware handler execution, every repository call goes to pool 0.
    /// When OutboxProcessorHost fires SessionExpirationHandler for the pool-1 event:
    ///   - ClientStore.FindClientByIdAsync queries pool 0 and the pool-1 client is invisible.
    ///   - Because no client is found, DefaultSessionCoordinationService skips RemoveAllAsync.
    ///   - The pool-1 persisted grants are NEVER cleaned up.
    ///
    /// The PoolAwareOutboxHandler decorator establishes the event's pool context before
    /// handler execution, so downstream stores resolve the correct pool.
    /// </summary>
    [Fact]
    public async Task session_expiration_in_non_default_pool_removes_grants_from_event_pool()
    {
        // Single SQLite in-memory DB shared across pools.
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataProtection();
        var dbName = $"pool_gap_{Guid.NewGuid():N}";
        var subscriberName = SubscriberName.Create("SessionExpiration");

        // Register DSOs and the subscriber (mirrors production wiring).
        services.AddSingleton<IOutboxSubscriber>(new TestSubscriber(subscriberName));
        services.AddStorageInternal(storage =>
            storage.AddSqliteStore(opt =>
                opt.ConnectionString = $"Data Source={dbName};Mode=Memory;Cache=Shared"));
        services.AddDsoRegistration<ServerSideSessionDso.V1>();
        services.AddDsoRegistration<ClientDso.V1>();
        services.AddDsoRegistration<PersistedGrantDso.V1>();

        var sp = services.BuildServiceProvider();
        var pooledStore = sp.GetRequiredService<IPooledStore>();
        await pooledStore.MigrateAsync(Ct.None);

        // Seed pool 1 with a client and a persisted grant for the same session.
        var pool1Storage = pooledStore.OpenPool(1);
        const string clientId = "coordinated_client";
        const string subjectId = "sub_pool1_gap";
        const string sessionId = "sid_pool1_gap";
        const string grantKey = "grant_pool1_gap";

        // Write the client to pool 1 only.
        var clientDso = BuildMinimalClientDso(clientId, coordinateLifetime: true);
        await pool1Storage.CreateAsync(
            UuidV7.New(),
            clientDso,
            [DataStorageKey.Create(ClientIdDskV1.Create(clientId))],
            BuildClientSearchFields(clientDso),
            Expiration.NoExpiration,
            [],
            Ct.None);

        // Write a refresh-token grant to pool 1 only.
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
        await pool1Storage.CreateAsync(
            grantId,
            grantDso,
            [DataStorageKey.Create(PersistedGrantKeyDskV1.Create(grantKey))],
            BuildGrantSearchFields(grantDso),
            Expiration.NoExpiration,
            [],
            Ct.None);

        // Write an outbox event in pool 1 for the session expiration.
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
        await pool1Storage.CreateAsync(
            sessionEntityId,
            sessionDso,
            [],
            new SearchFieldCollection([]),
            Expiration.NoExpiration,
            [outboxEvent],
            Ct.None);

        // Confirm the event is queued in pool 1.
        var queuedBefore = await pool1Storage.GetOutboxEventsForSubscriberAsync(subscriberName, 100, Ct.None);
        queuedBefore.Events.Count.ShouldBe(1);
        queuedBefore.Events[0].PoolId.Value.ShouldBe(1, "event was written in pool 1");

        // Build handler DI with pool-aware store factory (our fix).
        var poolContextAccessor = new PoolContextAccessor();
        var poolAwareStoreFactory = new DefaultStorageFactory(pooledStore, poolContextAccessor);

        var handlerServices = new ServiceCollection();
        handlerServices.AddLogging();
        handlerServices.AddSingleton(dataProtectionProvider);
        handlerServices.AddSingleton(_options);
        handlerServices.AddSingleton(TimeProvider.System);

        // Pool-aware store factory so repositories resolve the correct pool.
        handlerServices.AddSingleton<IStorageFactory>(poolAwareStoreFactory);

        // Real repositories backed by the pool-aware factory.
        handlerServices.AddScoped<ClientRepository>();
        handlerServices.AddScoped<PersistedGrantRepository>();

        // Real stores backed by those repositories.
        handlerServices.AddScoped<IClientStore, ClientStore>();
        handlerServices.AddScoped<IPersistedGrantStore, PersistedGrantStore>();

        // Null back-channel logout service as we only care about grant removal.
        handlerServices.AddSingleton<IBackChannelLogoutService>(new StubBackChannelLogoutClient());

        // Real DefaultSessionCoordinationService with real stores.
        handlerServices.AddScoped<ISessionCoordinationService>(svc =>
            new DefaultSessionCoordinationService(
                svc.GetRequiredService<IdentityServerOptions>(),
                svc.GetRequiredService<IPersistedGrantStore>(),
                svc.GetRequiredService<IClientStore>(),
                svc.GetRequiredService<IBackChannelLogoutService>(),
                TestLogger.Create<DefaultSessionCoordinationService>(),
                svc.GetRequiredService<TimeProvider>()));

        // Real SessionExpirationHandler, wrapped with PoolAwareOutboxHandler.
        handlerServices.AddSingleton<IPoolContextAccessor>(poolContextAccessor);
        handlerServices.AddKeyedTransient<IOutboxSubscriberHandler>(
            subscriberName.Value,
            (svc, _) => new PoolAwareOutboxHandler(
                new SessionExpirationHandler(
                    svc.GetRequiredService<IDataProtectionProvider>(),
                    svc.GetRequiredService<ISessionCoordinationService>(),
                    svc.GetRequiredService<IdentityServerOptions>(),
                    svc.GetRequiredService<TimeProvider>(),
                    TestLogger.Create<SessionExpirationHandler>()),
                svc.GetRequiredService<IPoolContextAccessor>()));

        var handlersServiceProvider = handlerServices.BuildServiceProvider();
        var scopeFactory = handlersServiceProvider.GetRequiredService<IServiceScopeFactory>();

        // The processor host reads outbox events from pool 1 (where we wrote them).
        var subscriber = sp.GetRequiredService<IOutboxSubscriber>();
        var host = new OutboxProcessorHost(
            new SimpleStorageFactory(pool1Storage),
            [subscriber],
            scopeFactory,
            _options,
            TimeProvider.System,
            _hostLogger);

        // Act
        await host.RunProcessorAsync(Ct.None);

        // Assert event consumed
        var queuedAfter = await pool1Storage.GetOutboxEventsForSubscriberAsync(subscriberName, 100, Ct.None);
        queuedAfter.Events.Count.ShouldBe(0, "event was consumed by the processor");

        // The grant in pool 1 should be removed because:
        //   - SessionExpirationHandler called DefaultSessionCoordinationService.ProcessExpirationAsync
        //   - ProcessExpirationAsync found the pool-1 client (via pool-aware store)
        //   - The client coordinates lifetime with user sessions
        //   - PersistedGrantStore.RemoveAllAsync removed the pool-1 grant
        var grantResult = await pool1Storage.TryReadAsync(
            PersistedGrantDso.EntityType,
            DataStorageKey.Create(PersistedGrantKeyDskV1.Create(grantKey)),
            Ct.None);
        grantResult.Found.ShouldBeFalse(
            "the pool-1 persisted grant should be removed when processing a pool-1 session expiration event");
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

    private sealed class TestSubscriber(SubscriberName name) : IOutboxSubscriber
    {
        public SubscriberName SubscriberName => name;
        public bool IsEnabled => true;
        public IReadOnlySet<OutboxEventName> EventNames { get; } =
            new HashSet<OutboxEventName> { OutboxEventName.EntityExpired };
        public IReadOnlySet<int> EntityTypeIds { get; } = new HashSet<int> { 2107 };
    }
}
