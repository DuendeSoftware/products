// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System.Collections.Concurrent;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.IntegrationTests.TestFramework;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Stores;
using Duende.Spaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Duende.IdentityServer.IntegrationTests.Hosting;

public sealed class SpacesInMemorySessionCleanupTests(WebServerFixture webServerFixture)
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task cleanup_removes_expired_sessions_in_every_space_and_sends_back_channel_logout_for_each()
    {
        var logouts = new ConcurrentBag<LogoutNotificationContext>();
        await using var host = await CreateHostAsync(new RecordingBackChannelLogoutService(logouts), enableSpaces: true);
        var sessions = new Dictionary<SpaceId, string>
        {
            [host.SpaceA.Id] = await StoreSessionAsync(host, host.SpaceA.Id, "client-a"),
            [host.SpaceB.Id] = await StoreSessionAsync(host, host.SpaceB.Id, "client-b"),
            [SpaceId.Default] = await StoreSessionAsync(host, SpaceId.Default, "client-default")
        };

        AdvancePastExpiry(host);
        await RunCleanupAsync(host);

        foreach (var (space, key) in sessions)
        {
            (await SessionExistsAsync(host, space, key)).ShouldBeFalse($"the expired session in {space} must be removed");
        }

        logouts.SelectMany(x => x.ClientIds).ShouldBe(["client-a", "client-b", "client-default"], ignoreOrder: true);
    }

    [Fact]
    public async Task a_failure_in_one_space_does_not_stop_the_cleanup_of_the_other_spaces()
    {
        var logouts = new ConcurrentBag<LogoutNotificationContext>();
        await using var host = await CreateHostAsync(new RecordingBackChannelLogoutService(logouts, failForClientId: "client-a"), enableSpaces: true);
        _ = await StoreSessionAsync(host, host.SpaceA.Id, "client-a");
        var keyB = await StoreSessionAsync(host, host.SpaceB.Id, "client-b");
        var keyDefault = await StoreSessionAsync(host, SpaceId.Default, "client-default");

        AdvancePastExpiry(host);
        await RunCleanupAsync(host);

        (await SessionExistsAsync(host, host.SpaceB.Id, keyB)).ShouldBeFalse();
        (await SessionExistsAsync(host, SpaceId.Default, keyDefault)).ShouldBeFalse();
        logouts.SelectMany(x => x.ClientIds).ShouldBe(["client-b", "client-default"], ignoreOrder: true);
    }

    [Fact]
    public async Task without_spaces_cleanup_removes_expired_sessions_and_sends_back_channel_logout_once()
    {
        var logouts = new ConcurrentBag<LogoutNotificationContext>();
        await using var host = await CreateHostAsync(new RecordingBackChannelLogoutService(logouts), enableSpaces: false);
        var key = await StoreSessionAsync(host, null, "client-default");

        AdvancePastExpiry(host);
        await RunCleanupAsync(host);

        (await SessionExistsAsync(host, null, key)).ShouldBeFalse();
        logouts.ShouldHaveSingleItem().ClientIds.ShouldBe(["client-default"]);
    }

    private Task<SpacesDataProtectionTestHost> CreateHostAsync(IBackChannelLogoutService logoutService, bool enableSpaces) =>
        SpacesDataProtectionTestHost.CreateAsync(webServerFixture, nameof(SpacesInMemorySessionCleanupTests),
            new SpacesDataProtectionTestHostOptions
            {
                EnableSpaces = enableSpaces,
                ServerSideSessions = true,
                InMemoryOperationalStores = true,
                BackChannelLogoutService = logoutService
            }, _ct);

    private static async Task<T> RunAsync<T>(SpacesDataProtectionTestHost host, SpaceId? space, Func<IServiceProvider, Task<T>> action)
    {
        if (space is { } spaceId)
        {
            return await host.RunInSpaceAsync(spaceId, action);
        }

        await using var scope = host.Services.CreateAsyncScope();
        return await action(scope.ServiceProvider);
    }

    private static Task<string> StoreSessionAsync(SpacesDataProtectionTestHost host, SpaceId? space, string clientId) =>
        RunAsync(host, space, async services =>
        {
            SpacesServerSideSessionTestHelpers.UseRequestContext(services);
            return await services.GetRequiredService<IServerSideTicketStore>().StoreAsync(SpacesServerSideSessionTestHelpers.CreateTicket([clientId]));
        });

    private Task<bool> SessionExistsAsync(SpacesDataProtectionTestHost host, SpaceId? space, string key) =>
        RunAsync(host, space, async services =>
            await services.GetRequiredService<IServerSideSessionStore>().GetSessionAsync(key, _ct) is not null);

    private static void AdvancePastExpiry(SpacesDataProtectionTestHost host) =>
        ((FakeTimeProvider)host.Services.GetRequiredService<TimeProvider>()).Advance(TimeSpan.FromHours(2));

    private Task RunCleanupAsync(SpacesDataProtectionTestHost host) =>
        host.Services.GetServices<IHostedService>().OfType<ServerSideSessionCleanupHost>().Single().RunAsync(_ct);

    private sealed class RecordingBackChannelLogoutService(ConcurrentBag<LogoutNotificationContext> logouts, string? failForClientId = null) : IBackChannelLogoutService
    {
        public Task SendLogoutNotificationsAsync(LogoutNotificationContext context, Ct ct)
        {
            if (failForClientId is not null && context.ClientIds.Contains(failForClientId))
            {
                throw new InvalidOperationException("Simulated failure.");
            }

            logouts.Add(context);
            return Task.CompletedTask;
        }
    }
}
