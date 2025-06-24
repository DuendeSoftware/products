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

public class HostBuilder_SessionTests(ITestOutputHelper output) : BffHostBuilderTestBase(output)
{
    private string _databaseName = Guid.NewGuid().ToString();

    [Fact]
    public async Task Can_run_without_session_storage()
    {
        var hostBuilder = Host.CreateApplicationBuilder();
        var bff = hostBuilder.AddBffApplication()
            .UsingTestServer();

        bff.EnableBffEndpoint();

        var host = hostBuilder.Build();
        await host.StartAsync();

        // By default, no session storage is configured. 
        var userSessionStore = host.Services.GetRequiredService<BffEndpointHostedService>().Services.GetService<IUserSessionStore>();
        userSessionStore.ShouldBeNull();

        await host.StopAsync();
    }
    [Fact]
    public async Task Can_enable_inmemory_sessions()
    {
        var hostBuilder = Host.CreateApplicationBuilder();

        var bff = hostBuilder.AddBffApplication()
            .UsingTestServer();

        bff.EnableBffEndpoint();

        bff.EnableServerSideSessions();

        var host = hostBuilder.Build();
        await host.StartAsync();

        // By default, if you use EnableServerSideSessions without any storage configuration,
        // you get the InMemoryUserSessionStore.
        var userSessionStore = host.Services.GetRequiredService<BffEndpointHostedService>().Services.GetRequiredService<IUserSessionStore>();
        var userSessionStore2 = host.Services.GetRequiredService<BffEndpointHostedService>().Services.GetRequiredService<IUserSessionStore>();
        userSessionStore.ShouldBeOfType<InMemoryUserSessionStore>();

        userSessionStore2.ShouldBe(userSessionStore, "should be singleton");

        await host.StopAsync();
    }

    [Fact]
    public void Cannot_call_add_server_side_sessions_twice()
    {
        var hostBuilder = Host.CreateApplicationBuilder();

        var bff = hostBuilder.AddBffApplication()
            .UsingTestServer();

        bff.EnableServerSideSessions();
        Should.Throw<InvalidOperationException>(() => bff.EnableServerSideSessions());
    }

    [Fact]
    public async Task Can_enable_inmemory_sessions_before_adding_endpoint()
    {
        var hostBuilder = Host.CreateApplicationBuilder();

        var bff = hostBuilder.AddBffApplication()
            .UsingTestServer();

        // Intentionally calling EnableServerSideSessions before EnableBffEndpoint
        bff.EnableServerSideSessions();
        bff.EnableBffEndpoint();

        var host = hostBuilder.Build();
        await host.StartAsync();

        // By default, if you use EnableServerSideSessions without any storage configuration,
        // you get the InMemoryUserSessionStore.
        var userSessionStore = host.Services.GetRequiredService<BffEndpointHostedService>().Services.GetRequiredService<IUserSessionStore>();
        var userSessionStore2 = host.Services.GetRequiredService<BffEndpointHostedService>().Services.GetRequiredService<IUserSessionStore>();
        userSessionStore.ShouldBeOfType<InMemoryUserSessionStore>();

        userSessionStore2.ShouldBe(userSessionStore, "should be singleton");

        await host.StopAsync();
    }

    [Fact]
    public async Task can_run_using_entity_framework()
    {
        var hostBuilder = Host.CreateApplicationBuilder();

        var bff = hostBuilder.AddBffApplication()
            .UsingTestServer();

        bff.EnableBffEndpoint();

        bff.EnableServerSideSessions()
            .UsingEntityFramework(opt => opt.UseInMemoryDatabase(_databaseName))
            ;

        var host = hostBuilder.Build();
        await host.StartAsync();

        // The BFFHostedService should use the UserSessionStore, which is registered by the WithEntityFramework
        var userSessionStore = host.Services.GetRequiredService<BffEndpointHostedService>().Services.GetRequiredService<IUserSessionStore>();
        userSessionStore.ShouldBeOfType<UserSessionStore>();

        await host.StopAsync();
    }

    [Fact]
    public async Task Will_cleanup_sessions()
    {
        var hostBuilder = Host.CreateApplicationBuilder();

        hostBuilder.Services.AddSingleton<TimeProvider>(The.Clock);

        var bff = hostBuilder.AddBffApplication()
            .UsingTestServer();

        bff.EnableBffEndpoint()
            .ConfigureApp(app => app.MapGet("/", () => "ok"))
            .ConfigureOpenIdConnect(opt =>
            {
                The.DefaultOpenIdConnectConfiguration(opt);
                opt.BackchannelHttpHandler = Context.Internet;
            });

        bff.EnableServerSideSessions()
            .UsingEntityFramework(opt => opt.UseInMemoryDatabase(_databaseName))
            .EnableSessionCleanupService()
            ;


        var host1 = hostBuilder.Build();
        var client = await InitializeAsync(host1);
        await client.Login();

        var sessionStore = host1.Services.GetRequiredService<IUserSessionStore>();
        // Get the partition key from the first host, which is where the session was created
        var partitionKey = host1.Services.GetRequiredService<BffEndpointHostedService>().Services.GetRequiredService<BuildUserSessionPartitionKey>()();

        // After logging in, there should be one session
        var sessions = await sessionStore.GetUserSessionsAsync(partitionKey, new UserSessionsFilter()
        {
            SubjectId = The.Sub
        });

        sessions.Count.ShouldBe(1);

        // Advance the clock to simulate session expiration
        AdvanceClock(TimeSpan.FromDays(20));

        // Run the cleanup host to remove expired sessions
        var cleanupHost = host1.Services.GetRequiredService<SessionCleanupHost>();
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
        // Spin up 2 host builders
        var hostBuilder1 = Host.CreateApplicationBuilder();

        var hostBuilder2 = Host.CreateApplicationBuilder();

        hostBuilder1.Services.AddSingleton<TimeProvider>(The.Clock);
        hostBuilder2.Services.AddSingleton<TimeProvider>(The.Clock);

        // Create a BFF with UI
        hostBuilder1.AddBffApplication()
            .UsingTestServer()
            .EnableBffEndpoint()
            .ConfigureApp(app => app.MapGet("/", () => "ok"))
            .ConfigureOpenIdConnect(opt =>
            {
                The.DefaultOpenIdConnectConfiguration(opt);
                opt.BackchannelHttpHandler = Context.Internet;
            })

            .BffApplicationBuilder
            .EnableServerSideSessions()
            // use the same db as other host
            .UsingEntityFramework(opt => opt.UseInMemoryDatabase(_databaseName))
            ;

        // Second hosted service hosts the cleanup job
        hostBuilder2.AddBffApplication()
            .EnableServerSideSessions()
            // Use same db as other host
            .UsingEntityFramework(opt => opt.UseInMemoryDatabase(_databaseName))
            .EnableSessionCleanupService();

        // Start everything
        var host1 = hostBuilder1.Build();
        using var host2 = hostBuilder2.Build();
        await host2.StartAsync();
        var client = await InitializeAsync(host1);

        await client.Login();

        var sessionStore = host1.Services.GetRequiredService<IUserSessionStore>();

        // Get the partition key from the first host, which is where the session was created
        var partitionKey = host1.Services.GetRequiredService<BffEndpointHostedService>().Services.GetRequiredService<BuildUserSessionPartitionKey>()();

        // After logging in, there should be one session
        var sessions = await sessionStore.GetUserSessionsAsync(partitionKey, new UserSessionsFilter()
        {
            SubjectId = The.Sub
        });

        sessions.Count.ShouldBe(1);

        // Advance the clock to simulate session expiration
        AdvanceClock(TimeSpan.FromDays(20));

        // Run the cleanup host to remove expired sessions
        var cleanupHost = host2.Services.GetRequiredService<SessionCleanupHost>();
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
        var hostBuilder = Host.CreateApplicationBuilder();

        var bff = hostBuilder.AddBffApplication()
            .UsingTestServer();

        bff.EnableBffEndpoint();

        bff.EnableServerSideSessions()
            .EnableSessionCleanupService()
            ;

        using var host = hostBuilder.Build();

        var exception = await Should.ThrowAsync<InvalidOperationException>(() => host.StartAsync());
        exception.Message.ShouldBe("No IUserSessionStoreCleanup is registered. Did you add session storage, such as EntityFramework?");

        await host.StopAsync();
    }

}
