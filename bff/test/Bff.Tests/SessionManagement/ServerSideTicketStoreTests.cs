// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.Tests.TestHosts;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.Bff.Tests.SessionManagement;

public class ServerSideTicketStoreTests : BffIntegrationTestBase
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;
    readonly InMemoryUserSessionStore _sessionStore = new();

    public ServerSideTicketStoreTests(ITestOutputHelper output) : base(output)
        => BffHost.OnConfigureServices += services =>
           {
               services.AddSingleton<IUserSessionStore>(_sessionStore);
           };

    [Fact]
    public async Task StoreAsync_should_remove_conflicting_entries_prior_to_creating_new_entry()
    {
        await BffHost.BffLoginAsync("alice");

        BffHost.BrowserClient.RemoveCookie("bff");
        var userSessionsFilter = new UserSessionsFilter { SubjectId = "alice" };
        var result = await _sessionStore.GetUserSessionsAsync(userSessionsFilter, _ct);
        result.Count.ShouldBe(1);

        await BffHost.BffOidcLoginAsync();

        result = await _sessionStore.GetUserSessionsAsync(userSessionsFilter, _ct);
        result.Count.ShouldBe(1);
    }
}
