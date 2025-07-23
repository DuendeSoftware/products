// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.Builder;
using Duende.Bff.EntityFramework;
using Duende.Bff.EntityFramework.Internal;
using Duende.Bff.SessionManagement.SessionStore;
using Duende.Bff.Tests.TestInfra;
using Microsoft.EntityFrameworkCore;
using Xunit.Abstractions;

namespace Duende.Bff.Tests.BffHostBuilder;

public class HostBuilder_SessionTests(ITestOutputHelper output) : BffTestBase(output)
{
    private string _databaseName = Guid.NewGuid().ToString();

    [Fact]
    public async Task Can_run_without_session_storage()
    {
        await InitializeAsync();
        // By default, no session storage is configured. 
        var userSessionStore = Bff.Server.Services.GetService<IUserSessionStore>();
        userSessionStore.ShouldBeNull();
    }

    [Fact]
    public async Task Can_enable_inmemory_sessions()
    {
        Bff.OnConfigureBff += bff =>
        {
            // Enable server-side sessions with in-memory storage
            bff.AddServerSideSessions();
        };
        await InitializeAsync();


        // By default, if you use EnableServerSideSessions without any storage configuration,
        // you get the InMemoryUserSessionStore.
        var userSessionStore = Bff.Server.Services.GetRequiredService<IUserSessionStore>();
        var userSessionStore2 = Bff.Server.Services.GetRequiredService<IUserSessionStore>();
        userSessionStore.ShouldBeOfType<InMemoryUserSessionStore>();

        userSessionStore2.ShouldBe(userSessionStore, "should be singleton");
    }


    [Fact]
    public async Task can_run_using_entity_framework()
    {
        Bff.OnConfigureBff += bff =>
        {
            // Enable server-side sessions with in-memory storage
            bff.AddEntityFrameworkServerSideSessions(opt => opt.UseInMemoryDatabase(_databaseName));

        };
        await InitializeAsync();

        // The bff should use the UserSessionStore, which is registered by the WithEntityFramework
        var userSessionStore = Bff.Server.Services.GetRequiredService<IUserSessionStore>();
        userSessionStore.ShouldBeOfType<UserSessionStore>();
    }

    [Fact]
    public async Task Will_cleanup_sessions()
    {
        Bff.OnConfigureBff += bff =>
        {
            // Enable server-side sessions with in-memory storage
            bff.AddEntityFrameworkServerSideSessions(opt => opt.UseInMemoryDatabase(_databaseName));
            bff.AddSessionCleanupBackgroundProcess();
            bff.ConfigureOpenIdConnect(The.DefaultOpenIdConnectConfiguration);
        };
        IdentityServer.AddClient(The.ClientId, Bff.Url());
        await InitializeAsync();

        await Bff.BrowserClient.Login();

        var sessionStore = Bff.Server.Services.GetRequiredService<IUserSessionStore>();
        // Get the partition key from the first host, which is where the session was created
        var partitionKey = Bff.Server.Services.GetRequiredService<BuildUserSessionPartitionKey>()();

        // After logging in, there should be one session
        var sessions = await sessionStore.GetUserSessionsAsync(partitionKey, new UserSessionsFilter()
        {
            SubjectId = The.Sub
        });

        sessions.Count.ShouldBe(1);

        // Advance the clock to simulate session expiration
        AdvanceClock(TimeSpan.FromDays(20));

        // Run the cleanup host to remove expired sessions
        var cleanupHost = Bff.Server.Services.GetRequiredService<SessionCleanupHost>();
        await cleanupHost.RunAsync();

        // After cleanup, there should be no sessions left
        sessions = await sessionStore.GetUserSessionsAsync(partitionKey, new UserSessionsFilter()
        {
            SubjectId = The.Sub
        });
        sessions.Count.ShouldBe(0);
    }


    [Fact]
    public async Task Will_cleanup_sessions_even_if_in_other_host()
    {
        var host2 = new BffTestHost(Context, IdentityServer);

        host2.OnConfigureBff += bff =>
        {
            // Enable server-side sessions with in-memory storage
            bff.AddEntityFrameworkServerSideSessions(opt => opt.UseInMemoryDatabase(_databaseName));
            bff.AddSessionCleanupBackgroundProcess();
        };

        Bff.OnConfigureBff += bff =>
        {
            // Enable server-side sessions with in-memory storage
            bff.AddEntityFrameworkServerSideSessions(opt => opt.UseInMemoryDatabase(_databaseName));
            bff.ConfigureOpenIdConnect(The.DefaultOpenIdConnectConfiguration);
        };
        IdentityServer.AddClient(The.ClientId, Bff.Url());
        await InitializeAsync();
        await host2.InitializeAsync();

        await Bff.BrowserClient.Login();

        var sessionStore = Bff.Server.Services.GetRequiredService<IUserSessionStore>();
        // Get the partition key from the first host, which is where the session was created
        var partitionKey = Bff.Server.Services.GetRequiredService<BuildUserSessionPartitionKey>()();

        // After logging in, there should be one session
        var sessions = await sessionStore.GetUserSessionsAsync(partitionKey, new UserSessionsFilter()
        {
            SubjectId = The.Sub
        });

        sessions.Count.ShouldBe(1);

        // Advance the clock to simulate session expiration
        AdvanceClock(TimeSpan.FromDays(20));

        // Run the cleanup host to remove expired sessions
        var cleanupHost = host2.Server.Services.GetRequiredService<SessionCleanupHost>();
        await cleanupHost.RunAsync();

        // After cleanup, there should be no sessions left
        sessions = await sessionStore.GetUserSessionsAsync(partitionKey, new UserSessionsFilter()
        {
            SubjectId = The.Sub
        });
        sessions.Count.ShouldBe(0);
    }


    [Fact]
    public async Task Cannot_add_cleanup_host_without_entity_framework()
    {
        Bff.OnConfigureBff += bff =>
        {
            // Enable server-side sessions with in-memory storage
            bff.AddSessionCleanupBackgroundProcess();
            bff.ConfigureOpenIdConnect(The.DefaultOpenIdConnectConfiguration);
        };
        IdentityServer.AddClient(The.ClientId, Bff.Url());

        var exception = await Should.ThrowAsync<InvalidOperationException>(InitializeAsync);
        exception.Message.ShouldBe("No IUserSessionStoreCleanup is registered. Did you add session storage, such as EntityFramework?");

    }

}
