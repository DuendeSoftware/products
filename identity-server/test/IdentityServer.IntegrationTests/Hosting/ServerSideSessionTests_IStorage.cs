// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.IntegrationTests.Common;
using Duende.IdentityServer.Stores;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.IdentityServer.IntegrationTests.Hosting;

/// <summary>
/// Runs server-side session protocol tests against IStorage-backed operational
/// stores using an in-memory SQLite database.
/// </summary>
public sealed class ServerSideSessionTests_IStorage : ServerSideSessionTestsBase, IAsyncLifetime
{
    public ServerSideSessionTests_IStorage()
    {
        // Must be registered after AddServerSideSessions() (in base) so that
        // AddStorage()'s server-side session store registration replaces the
        // in-memory store registered by AddServerSideSessions().
        _pipeline.OnPostConfigureServices += services =>
        {
            services.AddOperationalStorageForTesting();

            services.PostConfigure<IdentityServerOptions>(opts =>
            {
                opts.OutboxProcessor.FuzzStartup = false;
                opts.OutboxProcessor.ProcessInterval = TimeSpan.FromMilliseconds(100);

                opts.StoragePurge.EnablePurge = true;
                opts.StoragePurge.FuzzStartup = false;
                opts.StoragePurge.PurgeInterval = TimeSpan.FromMilliseconds(100);
            });
        };

        InitializePipeline();
    }

    public async ValueTask InitializeAsync() =>
        await _pipeline.MigrateStorageSchemaAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    /// <summary>
    /// The Storage-backed IServerSideSessionStore uses cursor-based pagination
    /// and does not compute TotalCount. This override adjusts assertions to
    /// account for that known behavioral divergence.
    /// </summary>
    public override async Task querysessions_on_ticket_store_should_use_session_store()
    {
        await _pipeline.LoginAsync("alice");
        _pipeline.RemoveLoginCookie();
        await _pipeline.LoginAsync("alice");
        _pipeline.RemoveLoginCookie();
        await _pipeline.LoginAsync("bob");
        _pipeline.RemoveLoginCookie();

        var ct = TestContext.Current.CancellationToken;

        var tickets = await _ticketService.QuerySessionsAsync(new SessionQuery { SubjectId = "alice" }, ct);
        var sessions = await _sessionStore.QuerySessionsAsync(ct, new SessionQuery { SubjectId = "alice" });

        // Storage-backed store does not support TotalCount (cursor-based pagination)
        tickets.TotalCount.ShouldBeNull();
        sessions.TotalCount.ShouldBeNull();

        // Results should still match
        tickets.Results.Count().ShouldBe(2);
        sessions.Results.Count().ShouldBe(2);
        tickets.Results.Select(x => x.SessionId).ShouldBe(sessions.Results.Select(x => x.SessionId));
    }
}
