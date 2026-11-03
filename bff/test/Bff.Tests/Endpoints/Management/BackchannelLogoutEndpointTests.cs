// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using Duende.AccessTokenManagement;
using Duende.AccessTokenManagement.OpenIdConnect;
using Duende.Bff.DynamicFrontends;
using Duende.Bff.SessionManagement.SessionStore;
using Duende.Bff.Tests.TestInfra;
using Duende.IdentityServer.Stores;

namespace Duende.Bff.Tests.Endpoints.Management;

public class BackchannelLogoutEndpointTests : BffTestBase
{
    public BackchannelLogoutEndpointTests() : base() => Bff.OnConfigureBff += bff =>
    {
        _ = bff.AddServerSideSessions();
    };

    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();

        foreach (var client in IdentityServer.Clients)
        {
            client.BackChannelLogoutUri = Bff.Url("/bff/backchannel").ToString();
            client.BackChannelLogoutSessionRequired = true;
        }
    }

    [Theory]
    [MemberData(nameof(AllSetups))]
    public async Task backchannel_logout_should_allow_anonymous(BffSetupType setup)
    {
        Bff.OnConfigureServices += svcs =>
        {
            _ = svcs.AddAuthorization(opts =>
            {
                opts.FallbackPolicy =
                    new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
                        .RequireAuthenticatedUser()
                        .Build();
            });
        };
        await ConfigureBff(setup);

        // if you call the endpoint without a token, it should return 400
        _ = await Bff.BrowserClient.PostAsync(Bff.Url("/bff/backchannel"), null)
            .CheckHttpStatusCode(HttpStatusCode.BadRequest);
    }

    [Theory]
    [MemberData(nameof(AllSetups))]
    public async Task backchannel_logout_endpoint_should_signout(BffSetupType setup)
    {
        await ConfigureBff(setup);

        _ = await Bff.BrowserClient.Login();

        await Bff.BrowserClient.RevokeIdentityServerSession();

        (await Bff.BrowserClient.GetIsUserLoggedInAsync()).ShouldBeFalse();
    }

    [Theory]
    [MemberData(nameof(AllSetups))]
    public async Task backchannel_logout_endpoint_for_incorrect_sub_should_not_logout_user(BffSetupType setup)
    {
        await ConfigureBff(setup);

        await Bff.BrowserClient.CreateIdentityServerSessionCookieAsync(IdentityServer, The.Sub, The.Sid);

        _ = await Bff.BrowserClient.Login();

        await Bff.BrowserClient.CreateIdentityServerSessionCookieAsync(IdentityServer, "different_sub", The.Sid);

        await Bff.BrowserClient.RevokeIdentityServerSession();

        (await Bff.BrowserClient.GetIsUserLoggedInAsync()).ShouldBeTrue();
    }

    [Theory]
    [MemberData(nameof(AllSetups))]
    public async Task backchannel_logout_endpoint_for_incorrect_sid_should_not_logout_user(BffSetupType setup)
    {
        await ConfigureBff(setup);

        _ = await Bff.BrowserClient.Login();

        await Bff.BrowserClient.CreateIdentityServerSessionCookieAsync(IdentityServer, The.Sub, "different_sid");

        await Bff.BrowserClient.RevokeIdentityServerSession();

        (await Bff.BrowserClient.GetIsUserLoggedInAsync()).ShouldBeTrue();
    }


    [Theory]
    [MemberData(nameof(AllSetups))]
    public async Task when_BackchannelLogoutAllUserSessions_is_false_backchannel_logout_should_only_logout_one_session(
        BffSetupType setup)
    {
        await ConfigureBff(setup);
        Bff.BffOptions.BackchannelLogoutAllUserSessions = false;

        await Bff.BrowserClient.CreateIdentityServerSessionCookieAsync(IdentityServer, The.Sub, The.Sid);

        _ = await Bff.BrowserClient.Login();

        // Set a Set-Cookie header to clear the "__Host-bff-auth" cookie
        Bff.BrowserClient.Cookies.Clear(Bff.Url());

        await Bff.BrowserClient.CreateIdentityServerSessionCookieAsync(IdentityServer, The.Sub, "different");

        _ = await Bff.BrowserClient.Login();

        (await GetUserSessions()).Count.ShouldBe(2);

        await Bff.BrowserClient.RevokeIdentityServerSession();

        {
            var sessions = await GetUserSessions();
            var session = sessions.Single();
            session.SessionId.ShouldBe(The.Sid);
        }
    }


    /// <summary>
    /// Get the user sessions for the provided frontend. If not provided, then the current frontend (if any) is used.
    /// </summary>
    /// <param name="forFrontend"></param>
    /// <returns></returns>
    private async Task<IReadOnlyCollection<UserSession>> GetUserSessions(BffFrontend? forFrontend = null)
    {
        using (var scope = Bff.ResolveForFrontend(forFrontend ?? CurrentFrontend))
        {
            var sessionStore = scope.Resolve<IUserSessionStore>();
            var partitionKey = scope.Resolve<BuildUserSessionPartitionKey>()();
            var userSessionsFilter = new UserSessionsFilter
            {
                SubjectId = The.Sub
            };
            return await sessionStore.GetUserSessionsAsync(partitionKey, userSessionsFilter);
        }
    }

    [Theory]
    [MemberData(nameof(AllSetups))]
    public async Task when_BackchannelLogoutAllUserSessions_is_false_backchannel_logout_should_logout_all_sessions(
        BffSetupType setup)
    {
        await ConfigureBff(setup);
        Bff.BffOptions.BackchannelLogoutAllUserSessions = true;

        await Bff.BrowserClient.CreateIdentityServerSessionCookieAsync(IdentityServer, The.Sub, The.Sid);

        _ = await Bff.BrowserClient.Login();

        // Set a Set-Cookie header to clear the "__Host-bff-auth" cookie
        Bff.BrowserClient.Cookies.Clear(Bff.Url());

        await Bff.BrowserClient.CreateIdentityServerSessionCookieAsync(IdentityServer, The.Sub, "different");

        _ = await Bff.BrowserClient.Login();

        (await GetUserSessions()).Count.ShouldBe(2);

        await Bff.BrowserClient.RevokeIdentityServerSession();

        (await GetUserSessions()).ShouldBeEmpty();
    }

    [Theory]
    [MemberData(nameof(AllSetups))]
    public async Task when_refresh_token_revocation_fails_backchannel_logout_should_still_revoke_remaining_tokens_and_delete_all_sessions(
        BffSetupType setup)
    {
        var tokenEndpoint = new FailFirstRevocationTracker();
        Bff.OnConfigureBff += bff => DecorateUserTokenEndpoint(bff.Services, tokenEndpoint);

        await ConfigureBff(setup, configureOpenIdConnect: options =>
        {
            The.DefaultOpenIdConnectConfiguration(options);
            options.Scope.Add("offline_access");
        });
        Bff.BffOptions.BackchannelLogoutAllUserSessions = true;
        Bff.BffOptions.RevokeRefreshTokenOnLogout = true;

        await Bff.BrowserClient.CreateIdentityServerSessionCookieAsync(IdentityServer, The.Sub, The.Sid);
        _ = await Bff.BrowserClient.Login();

        Bff.BrowserClient.Cookies.Clear(Bff.Url());

        await Bff.BrowserClient.CreateIdentityServerSessionCookieAsync(IdentityServer, The.Sub, "different");
        _ = await Bff.BrowserClient.Login();

        (await GetUserSessions()).Count.ShouldBe(2);
        (await CountRefreshTokenGrants()).ShouldBe(2);

        await Bff.BrowserClient.RevokeIdentityServerSession();

        // Revoking the first refresh token failed, but the second was still revoked...
        tokenEndpoint.RevocationAttempts.ShouldBe(2);
        (await CountRefreshTokenGrants()).ShouldBe(1);

        // ...the failure was reported...
        Bff.Logger.LogEntries.ShouldContain(e =>
            e.Contains("[Warning]") &&
            e.Contains("Failed to revoke refresh token") &&
            e.Contains(FailFirstRevocationTracker.FailureMessage));

        // ...and every server-side session was deleted anyway.
        (await GetUserSessions()).ShouldBeEmpty();
    }

    private async Task<int> CountRefreshTokenGrants()
    {
        var store = IdentityServer.Resolve<IPersistedGrantStore>();
        var grants = await store.GetAllAsync(new PersistedGrantFilter
        {
            SubjectId = The.Sub,
            Type = "refresh_token"
        }, Ct.None);
        return grants.Count();
    }

    /// <summary>
    /// Wraps the registered <see cref="IOpenIdConnectUserTokenEndpoint"/> so that the first
    /// refresh token revocation throws and the rest go to the real token endpoint.
    /// </summary>
    private static void DecorateUserTokenEndpoint(IServiceCollection services, FailFirstRevocationTracker tracker)
    {
        var original = services.Last(d => d.ServiceType == typeof(IOpenIdConnectUserTokenEndpoint));
        _ = services.Remove(original);
        services.Add(new ServiceDescriptor(
            typeof(IOpenIdConnectUserTokenEndpoint),
            sp => new FailFirstRevocationEndpoint(CreateOriginal(sp, original), tracker),
            original.Lifetime));
    }

    private static IOpenIdConnectUserTokenEndpoint CreateOriginal(IServiceProvider sp, ServiceDescriptor original) =>
        (IOpenIdConnectUserTokenEndpoint)(original.ImplementationInstance
            ?? original.ImplementationFactory?.Invoke(sp)
            ?? ActivatorUtilities.CreateInstance(sp, original.ImplementationType!));

    private sealed class FailFirstRevocationTracker
    {
        public const string FailureMessage = "Simulated refresh token revocation failure";

        private int _revocationAttempts;

        public int RevocationAttempts => Volatile.Read(ref _revocationAttempts);

        public bool RecordAttemptAndCheckShouldFail() => Interlocked.Increment(ref _revocationAttempts) == 1;
    }

    private sealed class FailFirstRevocationEndpoint(
        IOpenIdConnectUserTokenEndpoint inner,
        FailFirstRevocationTracker tracker) : IOpenIdConnectUserTokenEndpoint
    {
        public Task<TokenResult<UserToken>> RefreshAccessTokenAsync(
            UserRefreshToken userToken,
            UserTokenRequestParameters parameters,
            Ct ct = default) =>
            inner.RefreshAccessTokenAsync(userToken, parameters, ct);

        public Task RevokeRefreshTokenAsync(
            UserRefreshToken userToken,
            UserTokenRequestParameters parameters,
            Ct ct = default) =>
            tracker.RecordAttemptAndCheckShouldFail()
                ? throw new InvalidOperationException(FailFirstRevocationTracker.FailureMessage)
                : inner.RevokeRefreshTokenAsync(userToken, parameters, ct);
    }
}
