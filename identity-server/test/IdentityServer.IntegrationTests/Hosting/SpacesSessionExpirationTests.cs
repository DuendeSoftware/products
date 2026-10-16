// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System.Collections.Concurrent;
using Duende.IdentityServer.Hosting;
using Duende.IdentityServer.IntegrationTests.TestFramework;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Stores;
using Duende.Spaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Duende.IdentityServer.IntegrationTests.Hosting;

// With storage-backed server-side sessions and space binding on, expired sessions are handled by the outbox
// processor, which has no request and so no space of its own. The handler must run inside the space the event
// was written in, otherwise it cannot unprotect the ticket (it throws InvalidOperationException or
// CryptographicException, or the ticket is dropped). The outbox is driven on demand.
public sealed class SpacesSessionExpirationTests(WebServerFixture webServerFixture)
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task expired_session_in_a_named_space_is_removed_and_triggers_back_channel_logout_when_the_outbox_runs()
    {
        var logouts = new ConcurrentBag<LogoutNotificationContext>();
        await using var host = await SpacesDataProtectionTestHost.CreateAsync(webServerFixture, nameof(SpacesSessionExpirationTests),
            new SpacesDataProtectionTestHostOptions
            {
                ServerSideSessions = true,
                BackChannelLogoutService = new RecordingBackChannelLogoutService(logouts)
            }, _ct);

        var key = await host.RunInSpaceAsync(host.SpaceA.Id, async services =>
        {
            SpacesServerSideSessionTestHelpers.UseRequestContext(services);
            return await services.GetRequiredService<IServerSideTicketStore>().StoreAsync(SpacesServerSideSessionTestHelpers.CreateTicket([SpacesDataProtectionTestHost.ClientId]));
        });

        (await SessionExistsAsync(host, host.SpaceA.Id, key)).ShouldBeTrue("the session must exist before it expires");

        var clock = (FakeTimeProvider)host.Services.GetRequiredService<TimeProvider>();
        clock.Advance(TimeSpan.FromHours(2));

        var purge = host.Services.GetServices<IHostedService>().OfType<StoragePurgeHost>().Single();
        await purge.RunPurgeAsync(_ct);
        logouts.ShouldBeEmpty("back-channel logout is sent by the outbox handler, not by the purge");

        await host.RunOutboxProcessorAsync(_ct);

        (await SessionExistsAsync(host, host.SpaceA.Id, key)).ShouldBeFalse("the expired session must be removed");

        var logout = logouts.ShouldHaveSingleItem("the handler must unprotect the ticket in its space and trigger back-channel logout");
        logout.SubjectId.ShouldBe("alice");
        logout.SessionId.ShouldBe("sid-1");
        logout.LogoutReason.ShouldBe(LogoutNotificationReason.SessionExpiration);
        logout.ClientIds.ShouldBe([SpacesDataProtectionTestHost.ClientId]);
    }

    private async Task<bool> SessionExistsAsync(SpacesDataProtectionTestHost host, SpaceId space, string key) =>
        await host.RunInSpaceAsync(space, async services =>
            await services.GetRequiredService<IServerSideSessionStore>().GetSessionAsync(key, _ct) is not null);

    private sealed class RecordingBackChannelLogoutService(ConcurrentBag<LogoutNotificationContext> logouts) : IBackChannelLogoutService
    {
        public Task SendLogoutNotificationsAsync(LogoutNotificationContext context, Ct ct)
        {
            logouts.Add(context);
            return Task.CompletedTask;
        }
    }
}
