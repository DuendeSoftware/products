// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Stores;
using Microsoft.Extensions.Logging.Abstractions;

namespace UnitTests.Services.Default;

public class DefaultSessionReplacementServiceTests
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(true, false, true)]
    [InlineData(true, true, true)]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    [InlineData(null, false, false)]
    [InlineData(null, true, true)]
    public async Task ProcessReplacementAsync_honors_client_and_global_coordination_settings(
        bool? clientSetting, bool globalSetting, bool expectRevoked)
    {
        var options = new IdentityServerOptions();
        options.Authentication.CoordinateClientLifetimesWithUserSession = globalSetting;
        var grantStore = new InMemoryPersistedGrantStore();
        var service = new DefaultSessionReplacementService(
            options,
            new InMemoryClientStore([new Client { ClientId = "client", CoordinateLifetimeWithUserSession = clientSetting }]),
            grantStore,
            new NullLogger<DefaultSessionReplacementService>());

        await StoreGrantAsync(grantStore, "rt", "client", IdentityServerConstants.PersistedGrantTypes.RefreshToken, "sub", "sid");

        await service.ProcessReplacementAsync(new SessionReplacementContext
        {
            PreviousSubjectId = "sub",
            PreviousSessionId = "sid",
            PreviousSession = new UserSession
            {
                ClientIds = ["client"],
                SessionId = "sid",
                SubjectId = "sub"
            },
            NewSubjectId = "sub",
            NewSessionId = "new-sid"
        }, _ct);

        var remaining = await grantStore.GetAllAsync(new PersistedGrantFilter { SubjectId = "sub" }, _ct);
        remaining.Count.ShouldBe(expectRevoked ? 0 : 1);
    }

    [Fact]
    public async Task ProcessReplacementAsync_only_removes_grants_for_coordinated_clients_with_matching_subject_and_session()
    {
        var grantStore = new InMemoryPersistedGrantStore();
        var service = new DefaultSessionReplacementService(
            new IdentityServerOptions(),
            new InMemoryClientStore([
                new Client { ClientId = "coordinated", CoordinateLifetimeWithUserSession = true },
                new Client { ClientId = "opted_out", CoordinateLifetimeWithUserSession = false }
            ]),
            grantStore,
            new NullLogger<DefaultSessionReplacementService>());

        await StoreGrantAsync(grantStore, "rt1", "coordinated", IdentityServerConstants.PersistedGrantTypes.RefreshToken, "sub", "sid");
        await StoreGrantAsync(grantStore, "consent1", "coordinated", IdentityServerConstants.PersistedGrantTypes.UserConsent, "sub", "sid");
        await StoreGrantAsync(grantStore, "rt2", "opted_out", IdentityServerConstants.PersistedGrantTypes.RefreshToken, "sub", "sid");
        await StoreGrantAsync(grantStore, "rt3", "coordinated", IdentityServerConstants.PersistedGrantTypes.RefreshToken, "sub", "different-sid");
        await StoreGrantAsync(grantStore, "rt4", "coordinated", IdentityServerConstants.PersistedGrantTypes.RefreshToken, "different-sub", "sid");

        await service.ProcessReplacementAsync(new SessionReplacementContext
        {
            PreviousSubjectId = "sub",
            PreviousSessionId = "sid",
            PreviousSession = new UserSession
            {
                ClientIds = ["coordinated", "opted_out", "not_found"],
                SessionId = "sid",
                SubjectId = "sub"
            },
            NewSubjectId = "sub",
            NewSessionId = "new-sid"
        }, _ct);

        var remaining = (await grantStore.GetAllAsync(new PersistedGrantFilter { SubjectId = "sub" }, _ct))
            .Select(x => x.Key)
            .OrderBy(x => x)
            .ToList();
        remaining.AddRange((await grantStore.GetAllAsync(new PersistedGrantFilter { SubjectId = "different-sub" }, _ct)).Select(x => x.Key));
        remaining = remaining.OrderBy(x => x).ToList();
        remaining.ShouldBe(["consent1", "rt2", "rt3", "rt4"]);
    }

    [Theory]
    [InlineData(IdentityServerConstants.PersistedGrantTypes.RefreshToken)]
    [InlineData(IdentityServerConstants.PersistedGrantTypes.ReferenceToken)]
    [InlineData(IdentityServerConstants.PersistedGrantTypes.AuthorizationCode)]
    [InlineData(IdentityServerConstants.PersistedGrantTypes.BackChannelAuthenticationRequest)]
    public async Task ProcessReplacementAsync_removes_all_coordinated_token_types(string type)
    {
        var grantStore = new InMemoryPersistedGrantStore();
        var service = new DefaultSessionReplacementService(
            new IdentityServerOptions(),
            new InMemoryClientStore([new Client { ClientId = "coordinated", CoordinateLifetimeWithUserSession = true }]),
            grantStore,
            new NullLogger<DefaultSessionReplacementService>());

        await StoreGrantAsync(grantStore, "grant", "coordinated", type, "sub", "sid");

        await service.ProcessReplacementAsync(new SessionReplacementContext
        {
            PreviousSubjectId = "sub",
            PreviousSessionId = "sid",
            PreviousSession = new UserSession
            {
                ClientIds = ["coordinated"],
                SessionId = "sid",
                SubjectId = "sub"
            },
            NewSubjectId = "sub",
            NewSessionId = "new-sid"
        }, _ct);

        var remaining = await grantStore.GetAllAsync(new PersistedGrantFilter { SubjectId = "sub" }, _ct);
        remaining.Count.ShouldBe(0);
    }

    [Fact]
    public async Task ProcessReplacementAsync_does_not_call_RemoveAllAsync_when_no_clients_are_coordinated()
    {
        var recordingStore = new RecordingPersistedGrantStore();
        var service = new DefaultSessionReplacementService(
            new IdentityServerOptions(),
            new InMemoryClientStore([new Client { ClientId = "opted_out", CoordinateLifetimeWithUserSession = false }]),
            recordingStore,
            new NullLogger<DefaultSessionReplacementService>());

        await service.ProcessReplacementAsync(new SessionReplacementContext
        {
            PreviousSubjectId = "sub",
            PreviousSessionId = "sid",
            PreviousSession = new UserSession
            {
                ClientIds = ["opted_out"],
                SessionId = "sid",
                SubjectId = "sub"
            },
            NewSubjectId = "sub",
            NewSessionId = "new-sid"
        }, _ct);

        recordingStore.RemoveAllCalls.ShouldBeEmpty();
    }

    [Fact]
    public async Task ProcessReplacementAsync_revokes_grants_for_client_that_no_longer_exists()
    {
        var recordingStore = new RecordingPersistedGrantStore();
        var service = new DefaultSessionReplacementService(
            new IdentityServerOptions(),
            new InMemoryClientStore([]),
            recordingStore,
            new NullLogger<DefaultSessionReplacementService>());

        await service.ProcessReplacementAsync(new SessionReplacementContext
        {
            PreviousSubjectId = "sub",
            PreviousSessionId = "sid",
            PreviousSession = new UserSession
            {
                ClientIds = ["not_found"],
                SessionId = "sid",
                SubjectId = "sub"
            },
            NewSubjectId = "sub",
            NewSessionId = "new-sid"
        }, _ct);

        recordingStore.RemoveAllCalls.Count.ShouldBe(1);
        recordingStore.RemoveAllCalls[0].ClientIds.ShouldBe(["not_found"]);
    }

    [Theory]
    [InlineData("sub", null)]
    [InlineData("sub", "")]
    [InlineData("sub", " ")]
    [InlineData(null, "sid")]
    [InlineData(" ", "sid")]
    public async Task ProcessReplacementAsync_does_not_remove_grants_when_previous_subject_or_session_is_blank(string subjectId, string sessionId)
    {
        var recordingStore = new RecordingPersistedGrantStore();
        var service = new DefaultSessionReplacementService(
            new IdentityServerOptions(),
            new InMemoryClientStore([new Client { ClientId = "coordinated", CoordinateLifetimeWithUserSession = true }]),
            recordingStore,
            new NullLogger<DefaultSessionReplacementService>());

        await service.ProcessReplacementAsync(new SessionReplacementContext
        {
            PreviousSubjectId = subjectId!,
            PreviousSessionId = sessionId!,
            PreviousSession = new UserSession
            {
                ClientIds = ["coordinated", "not_found"],
                SessionId = sessionId!,
                SubjectId = subjectId!
            },
            NewSubjectId = "sub",
            NewSessionId = "new-sid"
        }, _ct);

        recordingStore.RemoveAllCalls.ShouldBeEmpty();
    }

    [Fact]
    public async Task ProcessReplacementAsync_does_not_call_RemoveAllAsync_when_ClientIds_is_empty()
    {
        var recordingStore = new RecordingPersistedGrantStore();
        var service = new DefaultSessionReplacementService(
            new IdentityServerOptions(),
            new InMemoryClientStore([]),
            recordingStore,
            new NullLogger<DefaultSessionReplacementService>());

        await service.ProcessReplacementAsync(new SessionReplacementContext
        {
            PreviousSubjectId = "sub",
            PreviousSessionId = "sid",
            PreviousSession = new UserSession
            {
                ClientIds = [],
                SessionId = "sid",
                SubjectId = "sub"
            },
            NewSubjectId = "sub",
            NewSessionId = "new-sid"
        }, _ct);

        recordingStore.RemoveAllCalls.ShouldBeEmpty();
    }

    [Fact]
    public async Task ProcessReplacementAsync_calls_RemoveAllAsync_once_with_expected_filter_shape()
    {
        var recordingStore = new RecordingPersistedGrantStore();
        var service = new DefaultSessionReplacementService(
            new IdentityServerOptions(),
            new InMemoryClientStore([new Client { ClientId = "coordinated", CoordinateLifetimeWithUserSession = true }]),
            recordingStore,
            new NullLogger<DefaultSessionReplacementService>());

        await service.ProcessReplacementAsync(new SessionReplacementContext
        {
            PreviousSubjectId = "sub",
            PreviousSessionId = "sid",
            PreviousSession = new UserSession
            {
                ClientIds = ["coordinated"],
                SessionId = "sid",
                SubjectId = "sub"
            },
            NewSubjectId = "new-sub",
            NewSessionId = "new-sid"
        }, _ct);

        recordingStore.RemoveAllCalls.Count.ShouldBe(1);
        var filter = recordingStore.RemoveAllCalls[0];
        filter.SubjectId.ShouldBe("sub");
        filter.SessionId.ShouldBe("sid");
        filter.ClientIds.ShouldBe(["coordinated"]);
        filter.Types.ShouldBe(
            [
                IdentityServerConstants.PersistedGrantTypes.RefreshToken,
                IdentityServerConstants.PersistedGrantTypes.ReferenceToken,
                IdentityServerConstants.PersistedGrantTypes.AuthorizationCode,
                IdentityServerConstants.PersistedGrantTypes.BackChannelAuthenticationRequest,
            ],
            ignoreOrder: true);
    }

    [Fact]
    public async Task ProcessReplacementAsync_removes_grants_for_coordinated_client_not_in_ClientIds()
    {
        var recordingStore = new RecordingPersistedGrantStore();
        recordingStore.Grants.Add(new PersistedGrant
        {
            Key = "ciba1",
            ClientId = "ciba_client",
            SubjectId = "sub",
            SessionId = "sid",
            Type = IdentityServerConstants.PersistedGrantTypes.BackChannelAuthenticationRequest,
            CreationTime = DateTime.UtcNow,
            Data = "{}"
        });
        recordingStore.Grants.Add(new PersistedGrant
        {
            Key = "rt1",
            ClientId = "ciba_client",
            SubjectId = "sub",
            SessionId = "sid",
            Type = IdentityServerConstants.PersistedGrantTypes.RefreshToken,
            CreationTime = DateTime.UtcNow,
            Data = "{}"
        });

        var service = new DefaultSessionReplacementService(
            new IdentityServerOptions(),
            new InMemoryClientStore([new Client { ClientId = "ciba_client", CoordinateLifetimeWithUserSession = true }]),
            recordingStore,
            new NullLogger<DefaultSessionReplacementService>());

        await service.ProcessReplacementAsync(new SessionReplacementContext
        {
            PreviousSubjectId = "sub",
            PreviousSessionId = "sid",
            PreviousSession = new UserSession
            {
                ClientIds = [],
                SessionId = "sid",
                SubjectId = "sub"
            },
            NewSubjectId = "sub",
            NewSessionId = "new-sid"
        }, _ct);

        recordingStore.RemoveAllCalls.Count.ShouldBe(1);
        recordingStore.RemoveAllCalls[0].ClientIds.ShouldBe(["ciba_client"]);
    }

    [Fact]
    public async Task ProcessReplacementAsync_does_not_remove_grants_for_opted_out_client_not_in_ClientIds()
    {
        var recordingStore = new RecordingPersistedGrantStore();
        recordingStore.Grants.Add(new PersistedGrant
        {
            Key = "ciba1",
            ClientId = "opted_out_ciba_client",
            SubjectId = "sub",
            SessionId = "sid",
            Type = IdentityServerConstants.PersistedGrantTypes.BackChannelAuthenticationRequest,
            CreationTime = DateTime.UtcNow,
            Data = "{}"
        });

        var service = new DefaultSessionReplacementService(
            new IdentityServerOptions(),
            new InMemoryClientStore([new Client { ClientId = "opted_out_ciba_client", CoordinateLifetimeWithUserSession = false }]),
            recordingStore,
            new NullLogger<DefaultSessionReplacementService>());

        await service.ProcessReplacementAsync(new SessionReplacementContext
        {
            PreviousSubjectId = "sub",
            PreviousSessionId = "sid",
            PreviousSession = new UserSession
            {
                ClientIds = [],
                SessionId = "sid",
                SubjectId = "sub"
            },
            NewSubjectId = "sub",
            NewSessionId = "new-sid"
        }, _ct);

        recordingStore.RemoveAllCalls.ShouldBeEmpty();
    }

    [Fact]
    public async Task ProcessReplacementAsync_ignores_grants_with_different_sid_or_sub()
    {
        var recordingStore = new RecordingPersistedGrantStore();
        recordingStore.Grants.Add(new PersistedGrant
        {
            Key = "ciba_diff_sid",
            ClientId = "ciba_client",
            SubjectId = "sub",
            SessionId = "different-sid",
            Type = IdentityServerConstants.PersistedGrantTypes.BackChannelAuthenticationRequest,
            CreationTime = DateTime.UtcNow,
            Data = "{}"
        });
        recordingStore.Grants.Add(new PersistedGrant
        {
            Key = "ciba_diff_sub",
            ClientId = "ciba_client",
            SubjectId = "different-sub",
            SessionId = "sid",
            Type = IdentityServerConstants.PersistedGrantTypes.BackChannelAuthenticationRequest,
            CreationTime = DateTime.UtcNow,
            Data = "{}"
        });

        var service = new DefaultSessionReplacementService(
            new IdentityServerOptions(),
            new InMemoryClientStore([new Client { ClientId = "ciba_client", CoordinateLifetimeWithUserSession = true }]),
            recordingStore,
            new NullLogger<DefaultSessionReplacementService>());

        await service.ProcessReplacementAsync(new SessionReplacementContext
        {
            PreviousSubjectId = "sub",
            PreviousSessionId = "sid",
            PreviousSession = new UserSession
            {
                ClientIds = [],
                SessionId = "sid",
                SubjectId = "sub"
            },
            NewSubjectId = "sub",
            NewSessionId = "new-sid"
        }, _ct);

        recordingStore.RemoveAllCalls.ShouldBeEmpty();
    }

    [Fact]
    public async Task ProcessReplacementAsync_revokes_coordinated_client_grants_when_PreviousSession_is_null()
    {
        var grantStore = new InMemoryPersistedGrantStore();
        var service = new DefaultSessionReplacementService(
            new IdentityServerOptions(),
            new InMemoryClientStore([new Client { ClientId = "coordinated", CoordinateLifetimeWithUserSession = true }]),
            grantStore,
            new NullLogger<DefaultSessionReplacementService>());

        await StoreGrantAsync(grantStore, "rt", "coordinated", IdentityServerConstants.PersistedGrantTypes.RefreshToken, "sub", "sid");

        await service.ProcessReplacementAsync(new SessionReplacementContext
        {
            PreviousSubjectId = "sub",
            PreviousSessionId = "sid",
            PreviousSession = null,
            NewSubjectId = "sub",
            NewSessionId = "new-sid"
        }, _ct);

        var remaining = await grantStore.GetAllAsync(new PersistedGrantFilter { SubjectId = "sub" }, _ct);
        remaining.Count.ShouldBe(0);
    }

    [Fact]
    public async Task ProcessReplacementAsync_throws_for_null_context()
    {
        var service = new DefaultSessionReplacementService(
            new IdentityServerOptions(),
            new InMemoryClientStore([]),
            new InMemoryPersistedGrantStore(),
            new NullLogger<DefaultSessionReplacementService>());

        await Should.ThrowAsync<ArgumentNullException>(() => service.ProcessReplacementAsync(null!, _ct));
    }

    private Task StoreGrantAsync(IPersistedGrantStore store, string key, string clientId, string type, string subjectId, string sessionId) =>
        store.StoreAsync(new PersistedGrant
        {
            Key = key,
            ClientId = clientId,
            SubjectId = subjectId,
            SessionId = sessionId,
            Type = type,
            CreationTime = DateTime.UtcNow,
            Data = "{}"
        }, _ct);

    private sealed class RecordingPersistedGrantStore : IPersistedGrantStore
    {
        public List<PersistedGrant> Grants { get; } = [];

        public List<PersistedGrantFilter> RemoveAllCalls { get; } = [];

        public Task StoreAsync(PersistedGrant grant, Ct ct) => Task.CompletedTask;

        public Task<PersistedGrant> GetAsync(string key, Ct ct) => Task.FromResult<PersistedGrant>(null);

        public Task<IReadOnlyCollection<PersistedGrant>> GetAllAsync(PersistedGrantFilter filter, Ct ct)
        {
            var result = Grants.Where(g =>
                (filter.SubjectId == null || g.SubjectId == filter.SubjectId) &&
                (filter.SessionId == null || g.SessionId == filter.SessionId) &&
                (filter.Types.Count == 0 || filter.Types.Contains(g.Type))).ToList();
            return Task.FromResult<IReadOnlyCollection<PersistedGrant>>(result);
        }

        public Task RemoveAsync(string key, Ct ct) => Task.CompletedTask;

        public Task RemoveAllAsync(PersistedGrantFilter filter, Ct ct)
        {
            RemoveAllCalls.Add(filter);
            return Task.CompletedTask;
        }
    }
}
