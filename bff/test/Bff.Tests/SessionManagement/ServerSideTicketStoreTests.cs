// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.SessionManagement.SessionStore;
using Duende.Bff.Tests.TestInfra;
using Xunit.Abstractions;

namespace Duende.Bff.Tests.SessionManagement;

public class ServerSideTicketStoreTests : BffTestBase
{
    private readonly InMemoryUserSessionStore _sessionStore = new();

    public ServerSideTicketStoreTests(ITestOutputHelper output) : base(output) => Bff.OnConfigureServices += services =>
                                                                                       {
                                                                                           services.AddSingleton<IUserSessionStore>(_sessionStore);
                                                                                       };

    [Theory, MemberData(nameof(AllSetups))]
    public async Task StoreAsync_should_remove_conflicting_entries_prior_to_creating_new_entry(BffSetupType setup)
    {
        Bff.OnConfigureBff += bff => bff.AddServerSideSessions();
        ConfigureBff(setup);
        await InitializeAsync();
        await Bff.BrowserClient.Login();

        Bff.BrowserClient.Cookies.Clear(Bff.Url());
        (await _sessionStore.GetUserSessionsAsync(new UserSessionsFilter { SubjectId = The.Sub })).Count().ShouldBe(1);

        await Bff.BrowserClient.Login();

        (await _sessionStore.GetUserSessionsAsync(new UserSessionsFilter { SubjectId = The.Sub })).Count().ShouldBe(1);
    }
}
