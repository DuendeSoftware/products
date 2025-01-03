// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.Tests.TestFramework;
using Duende.Bff.Tests.TestHosts;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Duende.Bff.Tests.SessionManagement
{
    public class ServerSideTicketStoreTests : BffIntegrationTestBase
    {
        InMemoryUserSessionStore _sessionStore = new InMemoryUserSessionStore();

        public ServerSideTicketStoreTests()
        {
            BffHost.OnConfigureServices += services =>
            {
                services.AddSingleton<IUserSessionStore>(_sessionStore);
            };
            BffHost.InitializeAsync().Wait();
        }

        [Fact]
        public async Task StoreAsync_should_remove_conflicting_entries_prior_to_creating_new_entry()
        {
            await BffHost.BffLoginAsync("alice");

            BffHost.BrowserClient.RemoveCookie("bff");
            (await _sessionStore.GetUserSessionsAsync(new UserSessionsFilter { SubjectId = "alice" })).Count().Should().Be(1);
            
            await BffHost.BffOidcLoginAsync();

            (await _sessionStore.GetUserSessionsAsync(new UserSessionsFilter { SubjectId = "alice" })).Count().Should().Be(1);
        }
    }
}
