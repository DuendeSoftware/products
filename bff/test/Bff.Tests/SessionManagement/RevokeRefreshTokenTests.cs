// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.Tests.TestInfra;
using Duende.IdentityServer.Stores;
using Xunit.Abstractions;

namespace Duende.Bff.Tests.SessionManagement;

public class RevokeRefreshTokenTests(ITestOutputHelper output) : BffTestBase(output)
{
    [Theory, MemberData(nameof(AllSetups))]
    public async Task logout_should_revoke_refreshtoken(BffSetupType setup)
    {
        await ConfigureBff(setup, configureOpenIdConnect: options =>
        {
            The.DefaultOpenIdConnectConfiguration(options);
            options.Scope.Add("offline_access");
        });

        await Bff.BrowserClient.Login();

        {
            var store = IdentityServer.Resolve<IPersistedGrantStore>();
            var grants = await store.GetAllAsync(new PersistedGrantFilter
            {
                SubjectId = The.Sub
            });
            var rt = grants.Single(x => x.Type == "refresh_token");
            rt.ShouldNotBeNull();
        }

        await Bff.BrowserClient.Logout();

        {
            var store = IdentityServer.Resolve<IPersistedGrantStore>();
            var grants = await store.GetAllAsync(new PersistedGrantFilter
            {
                SubjectId = The.Sub
            });
            grants.ShouldBeEmpty();
        }
    }

    [Theory, MemberData(nameof(AllSetups))]
    public async Task when_setting_disabled_logout_should_not_revoke_refreshtoken(BffSetupType setup)
    {
        await ConfigureBff(setup, configureOpenIdConnect: options =>
        {
            The.DefaultOpenIdConnectConfiguration(options);
            options.Scope.Add("offline_access");
        });

        Bff.BffOptions.RevokeRefreshTokenOnLogout = false;

        await Bff.BrowserClient.Login();

        {
            var store = IdentityServer.Resolve<IPersistedGrantStore>();
            var grants = await store.GetAllAsync(new PersistedGrantFilter
            {
                SubjectId = The.Sub
            });
            var rt = grants.Single(x => x.Type == "refresh_token");
            rt.ShouldNotBeNull();
        }

        await Bff.BrowserClient.Logout();

        {
            var store = IdentityServer.Resolve<IPersistedGrantStore>();
            var grants = await store.GetAllAsync(new PersistedGrantFilter
            {
                SubjectId = The.Sub
            });
            var rt = grants.Single(x => x.Type == "refresh_token");
            rt.ShouldNotBeNull();
        }
    }

    [Theory, MemberData(nameof(AllSetups))]
    public async Task backchannel_logout_endpoint_should_revoke_refreshtoken(BffSetupType setup)
    {
        Bff.OnConfigureBff += bff => bff.AddServerSideSessions();

        await ConfigureBff(setup, configureOpenIdConnect: options =>
        {
            The.DefaultOpenIdConnectConfiguration(options);
            options.Scope.Add("offline_access");
        });

        foreach (var client in IdentityServer.Clients)
        {
            client.BackChannelLogoutUri = Bff.Url("/bff/backchannel").ToString();
            client.BackChannelLogoutSessionRequired = true;
        }

        await Bff.BrowserClient.Login();

        {
            var store = IdentityServer.Resolve<IPersistedGrantStore>();
            var grants = await store.GetAllAsync(new PersistedGrantFilter
            {
                SubjectId = The.Sub
            });
            var rt = grants.Single(x => x.Type == "refresh_token");
            rt.ShouldNotBeNull();
        }

        await Bff.BrowserClient.RevokeIdentityServerSession();

        {
            var store = IdentityServer.Resolve<IPersistedGrantStore>();
            var grants = await store.GetAllAsync(new PersistedGrantFilter
            {
                SubjectId = The.Sub
            });
            grants.ShouldBeEmpty();
        }
    }

    [Theory, MemberData(nameof(AllSetups))]
    public async Task when_setting_disabled_backchannel_logout_endpoint_should_not_revoke_refreshtoken(
        BffSetupType setup)
    {
        await ConfigureBff(setup, configureOpenIdConnect: options =>
        {
            The.DefaultOpenIdConnectConfiguration(options);
            options.Scope.Add("offline_access");
        });

        Bff.OnConfigureBff += bff => bff.AddServerSideSessions();


        Bff.BffOptions.RevokeRefreshTokenOnLogout = false;

        foreach (var client in IdentityServer.Clients)
        {
            client.BackChannelLogoutUri = Bff.Url("/bff/backchannel").ToString();
            client.BackChannelLogoutSessionRequired = true;
        }

        await Bff.BrowserClient.Login();

        {
            var store = IdentityServer.Resolve<IPersistedGrantStore>();
            var grants = await store.GetAllAsync(new PersistedGrantFilter
            {
                SubjectId = The.Sub
            });
            var rt = grants.Single(x => x.Type == "refresh_token");
            rt.ShouldNotBeNull();
        }

        await Bff.BrowserClient.RevokeIdentityServerSession();

        {
            var store = IdentityServer.Resolve<IPersistedGrantStore>();
            var grants = await store.GetAllAsync(new PersistedGrantFilter
            {
                SubjectId = The.Sub
            });
            var rt = grants.Single(x => x.Type == "refresh_token");
            rt.ShouldNotBeNull();
        }
    }
}
