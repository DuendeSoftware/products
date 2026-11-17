// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Saml;
using Duende.IdentityServer.Stores;
using Duende.Spaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace UnitTests.Stores.InMemory;

public sealed class InMemoryStoreSpaceIsolationTests
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;
    private readonly SpaceId _spaceA = SpaceId.New();
    private readonly SpaceId _spaceB = SpaceId.New();

    private static ServiceProvider BuildProvider(bool withSpaces)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        var builder = services.AddIdentityServer();
        builder.AddServerSideSessions();
        builder.AddSaml();

        if (withSpaces)
        {
            builder.AddSpaces(_ => { });
        }

        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task persisted_grants_are_isolated_per_space()
    {
        using var provider = BuildProvider(withSpaces: true);
        var accessor = provider.GetRequiredService<ISpaceContextAccessor>();
        var store = provider.GetRequiredService<IPersistedGrantStore>();

        using (accessor.SetSpace(_spaceA))
        {
            await store.StoreAsync(new PersistedGrant { Key = "k", SubjectId = "sub", ClientId = "c", Type = "t" }, _ct);
        }

        using (accessor.SetSpace(_spaceB))
        {
            (await store.GetAsync("k", _ct)).ShouldBeNull();
            (await store.GetAllAsync(new PersistedGrantFilter { SubjectId = "sub" }, _ct)).ShouldBeEmpty();

            await store.RemoveAllAsync(new PersistedGrantFilter { SubjectId = "sub" }, _ct);
            await store.RemoveAsync("k", _ct);
        }

        using (accessor.SetSpace(_spaceA))
        {
            (await store.GetAsync("k", _ct)).ShouldNotBeNull();
        }
    }

    [Fact]
    public async Task device_flow_data_is_isolated_per_space()
    {
        using var provider = BuildProvider(withSpaces: true);
        var accessor = provider.GetRequiredService<ISpaceContextAccessor>();
        var store = provider.GetRequiredService<IDeviceFlowStore>();

        using (accessor.SetSpace(_spaceA))
        {
            await store.StoreDeviceAuthorizationAsync("device", "user", new DeviceCode { ClientId = "c" }, _ct);
        }

        using (accessor.SetSpace(_spaceB))
        {
            (await store.FindByDeviceCodeAsync("device", _ct)).ShouldBeNull();
            (await store.FindByUserCodeAsync("user", _ct)).ShouldBeNull();

            await store.UpdateByUserCodeAsync("user", new DeviceCode { ClientId = "changed" }, _ct);
            await store.RemoveByDeviceCodeAsync("device", _ct);
        }

        using (accessor.SetSpace(_spaceA))
        {
            var found = await store.FindByDeviceCodeAsync("device", _ct);
            found.ShouldNotBeNull();
            found.ClientId.ShouldBe("c");
        }
    }

    [Fact]
    public async Task pushed_authorization_requests_are_isolated_per_space()
    {
        using var provider = BuildProvider(withSpaces: true);
        var accessor = provider.GetRequiredService<ISpaceContextAccessor>();
        var store = provider.GetRequiredService<IPushedAuthorizationRequestStore>();

        using (accessor.SetSpace(_spaceA))
        {
            await store.StoreAsync(new PushedAuthorizationRequest { ReferenceValueHash = "hash", Parameters = "p" }, _ct);
        }

        using (accessor.SetSpace(_spaceB))
        {
            (await store.GetByHashAsync("hash", _ct)).ShouldBeNull();
            await store.ConsumeByHashAsync("hash", _ct);
        }

        using (accessor.SetSpace(_spaceA))
        {
            (await store.GetByHashAsync("hash", _ct)).ShouldNotBeNull();
        }
    }

    [Fact]
    public async Task server_side_sessions_are_isolated_per_space()
    {
        using var provider = BuildProvider(withSpaces: true);
        var accessor = provider.GetRequiredService<ISpaceContextAccessor>();
        var store = provider.GetRequiredService<IServerSideSessionStore>();

        using (accessor.SetSpace(_spaceA))
        {
            await store.CreateSessionAsync(new ServerSideSession { Key = "k", SubjectId = "sub", SessionId = "sid", Scheme = "s", Ticket = "t" }, _ct);
        }

        using (accessor.SetSpace(_spaceB))
        {
            (await store.GetSessionAsync("k", _ct)).ShouldBeNull();
            (await store.GetSessionsAsync(new SessionFilter { SubjectId = "sub" }, _ct)).ShouldBeEmpty();
            (await store.QuerySessionsAsync(_ct)).Results.ShouldBeEmpty();

            await store.DeleteSessionsAsync(new SessionFilter { SubjectId = "sub" }, _ct);
            await store.DeleteSessionAsync("k", _ct);
        }

        using (accessor.SetSpace(_spaceA))
        {
            (await store.GetSessionAsync("k", _ct)).ShouldNotBeNull();
            (await store.QuerySessionsAsync(_ct)).Results.Count().ShouldBe(1);
        }
    }

    [Fact]
    public async Task saml_signin_state_is_isolated_per_space()
    {
        using var provider = BuildProvider(withSpaces: true);
        var accessor = provider.GetRequiredService<ISpaceContextAccessor>();
        var store = provider.GetRequiredService<ISamlSigninStateStore>();
        Guid id;

        using (accessor.SetSpace(_spaceA))
        {
            id = await store.StoreSigninRequestStateAsync(new SamlAuthenticationState
            {
                ServiceProviderEntityId = "https://sp.example.com",
                AssertionConsumerService = new IndexedEndpoint { Location = "https://sp.example.com/acs", Binding = SamlBinding.HttpPost },
                CreatedUtc = DateTimeOffset.UtcNow,
                ExpiresAtUtc = DateTime.UtcNow.AddMinutes(15)
            }, _ct);
        }

        using (accessor.SetSpace(_spaceB))
        {
            (await store.RetrieveSigninRequestStateAsync(id, _ct)).ShouldBeNull();
            await store.RemoveSigninRequestStateAsync(id, _ct);
        }

        using (accessor.SetSpace(_spaceA))
        {
            (await store.RetrieveSigninRequestStateAsync(id, _ct)).ShouldNotBeNull();
        }
    }

    [Fact]
    public async Task saml_logout_sessions_are_isolated_per_space_including_the_request_index()
    {
        using var provider = BuildProvider(withSpaces: true);
        var accessor = provider.GetRequiredService<ISpaceContextAccessor>();
        var store = provider.GetRequiredService<ISamlLogoutSessionStore>();

        using (accessor.SetSpace(_spaceA))
        {
            await store.StoreAsync(new SamlLogoutSession
            {
                LogoutId = "logout",
                ExpectedResponses = new Dictionary<string, ExpectedSpLogout> { ["req"] = new("https://sp.example.com") },
                CreatedUtc = DateTimeOffset.UtcNow,
                ExpiresAtUtc = DateTime.UtcNow.AddMinutes(5)
            }, _ct);
        }

        using (accessor.SetSpace(_spaceB))
        {
            (await store.GetByLogoutIdAsync("logout", _ct)).ShouldBeNull();
            (await store.TryRecordResponseAsync("req", "https://sp.example.com", true, _ct)).ShouldBeFalse();
            await store.RemoveAsync("logout", _ct);
        }

        using (accessor.SetSpace(_spaceA))
        {
            (await store.GetByLogoutIdAsync("logout", _ct)).ShouldNotBeNull();
            (await store.TryRecordResponseAsync("req", "https://sp.example.com", true, _ct)).ShouldBeTrue();
        }
    }

    [Fact]
    public async Task requests_without_a_space_share_data_with_the_default_space()
    {
        using var provider = BuildProvider(withSpaces: true);
        var accessor = provider.GetRequiredService<ISpaceContextAccessor>();
        var store = provider.GetRequiredService<IPersistedGrantStore>();

        await store.StoreAsync(new PersistedGrant { Key = "k" }, _ct);

        using (accessor.SetSpace(SpaceId.Default))
        {
            (await store.GetAsync("k", _ct)).ShouldNotBeNull();
        }
    }

    [Fact]
    public async Task without_spaces_the_stores_work_and_use_the_default_space()
    {
        using var provider = BuildProvider(withSpaces: false);
        var store = provider.GetRequiredService<IPersistedGrantStore>();

        await store.StoreAsync(new PersistedGrant { Key = "k" }, _ct);

        (await store.GetAsync("k", _ct)).ShouldNotBeNull();
        provider.GetRequiredService<InMemoryStoreState>().CurrentSpace().ShouldBe(SpaceId.Default);
    }

    [Fact]
    public void the_registered_state_follows_the_current_space_when_spaces_is_added()
    {
        using var provider = BuildProvider(withSpaces: true);
        var accessor = provider.GetRequiredService<ISpaceContextAccessor>();
        var state = provider.GetRequiredService<InMemoryStoreState>();

        using (accessor.SetSpace(_spaceA))
        {
            state.CurrentSpace().ShouldBe(_spaceA);
        }

        state.CurrentSpace().ShouldBe(SpaceId.Default);
    }

    [Fact]
    public async Task reading_in_a_space_without_data_does_not_create_a_partition()
    {
        // The session cleanup visits every partition, so empty spaces created by reads would be cleaned (and
        // license-checked) on every run.
        using var provider = BuildProvider(withSpaces: true);
        var accessor = provider.GetRequiredService<ISpaceContextAccessor>();
        var store = (InMemoryServerSideSessionStore)provider.GetRequiredService<IServerSideSessionStore>();

        using (accessor.SetSpace(_spaceA))
        {
            (await store.GetSessionAsync("k", _ct)).ShouldBeNull();
            (await store.GetSessionsAsync(new SessionFilter { SubjectId = "sub" }, _ct)).ShouldBeEmpty();
            (await store.QuerySessionsAsync(_ct)).Results.ShouldBeEmpty();
            (await store.GetAndRemoveExpiredSessionsAsync(10, _ct)).ShouldBeEmpty();
            await store.DeleteSessionAsync("k", _ct);
            await store.DeleteSessionsAsync(new SessionFilter { SubjectId = "sub" }, _ct);
        }

        store.SpaceIds.ShouldBeEmpty();

        using (accessor.SetSpace(_spaceA))
        {
            await store.CreateSessionAsync(new ServerSideSession { Key = "k", SubjectId = "sub", SessionId = "sid", Scheme = "s", Ticket = "t" }, _ct);
        }

        store.SpaceIds.ShouldBe([_spaceA]);
    }
}
