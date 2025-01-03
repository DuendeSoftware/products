// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.Tests.TestHosts;
using Duende.IdentityServer.Stores;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Duende.Bff.Tests.SessionManagement
{
    public class RevokeRefreshTokenTests : BffIntegrationTestBase
    {
        [Fact]
        public async Task logout_should_revoke_refreshtoken()
        {
            await BffHost.BffLoginAsync("alice", "sid");

            {
                var store = IdentityServerHost.Resolve<IPersistedGrantStore>();
                var grants = await store.GetAllAsync(new PersistedGrantFilter
                {
                    SubjectId = "alice"
                });
                var rt = grants.Single(x => x.Type == "refresh_token");
                rt.Should().NotBeNull();
            }

            await BffHost.BffLogoutAsync("sid");

            {
                var store = IdentityServerHost.Resolve<IPersistedGrantStore>();
                var grants = await store.GetAllAsync(new PersistedGrantFilter
                {
                    SubjectId = "alice"
                });
                grants.Should().BeEmpty();
            }
        }
        
        [Fact]
        public async Task when_setting_disabled_logout_should_not_revoke_refreshtoken()
        {
            BffHost.OnConfigureServices += svcs => {
                svcs.Configure<BffOptions>(options => {
                    options.RevokeRefreshTokenOnLogout = false;
                });
            };
            await BffHost.InitializeAsync();

            await BffHost.BffLoginAsync("alice", "sid");

            {
                var store = IdentityServerHost.Resolve<IPersistedGrantStore>();
                var grants = await store.GetAllAsync(new PersistedGrantFilter
                {
                    SubjectId = "alice"
                });
                var rt = grants.Single(x => x.Type == "refresh_token");
                rt.Should().NotBeNull();
            }

            await BffHost.BffLogoutAsync("sid");

            {
                var store = IdentityServerHost.Resolve<IPersistedGrantStore>();
                var grants = await store.GetAllAsync(new PersistedGrantFilter
                {
                    SubjectId = "alice"
                });
                var rt = grants.Single(x => x.Type == "refresh_token");
                rt.Should().NotBeNull();
            }
        }

        [Fact]
        public async Task backchannel_logout_endpoint_should_revoke_refreshtoken()
        {
            await BffHost.BffLoginAsync("alice", "sid123");
            
            {
                var store = IdentityServerHost.Resolve<IPersistedGrantStore>();
                var grants = await store.GetAllAsync(new PersistedGrantFilter
                {
                    SubjectId = "alice"
                });
                var rt = grants.Single(x => x.Type == "refresh_token");
                rt.Should().NotBeNull();
            }

            await IdentityServerHost.RevokeSessionCookieAsync();

            {
                var store = IdentityServerHost.Resolve<IPersistedGrantStore>();
                var grants = await store.GetAllAsync(new PersistedGrantFilter
                {
                    SubjectId = "alice"
                });
                var rt = grants.Should().BeEmpty();
            }
        }

        [Fact]
        public async Task when_setting_disabled_backchannel_logout_endpoint_should_not_revoke_refreshtoken()
        {
            BffHost.OnConfigureServices += svcs => {
                svcs.Configure<BffOptions>(options => {
                    options.RevokeRefreshTokenOnLogout = false;
                });
            };
            await BffHost.InitializeAsync();
            
            await BffHost.BffLoginAsync("alice", "sid123");

            {
                var store = IdentityServerHost.Resolve<IPersistedGrantStore>();
                var grants = await store.GetAllAsync(new PersistedGrantFilter
                {
                    SubjectId = "alice"
                });
                var rt = grants.Single(x => x.Type == "refresh_token");
                rt.Should().NotBeNull();
            }

            await IdentityServerHost.RevokeSessionCookieAsync();

            {
                var store = IdentityServerHost.Resolve<IPersistedGrantStore>();
                var grants = await store.GetAllAsync(new PersistedGrantFilter
                {
                    SubjectId = "alice"
                });
                var rt = grants.Single(x => x.Type == "refresh_token");
                rt.Should().NotBeNull();
            }
        }
    }
}
