// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Stores;
using Microsoft.Extensions.Logging.Abstractions;
using UnitTests.Endpoints.EndSession;

namespace UnitTests.Services.Default;

public class DefaultSessionCoordinationServiceTests
{
    public DefaultSessionCoordinationService Service;
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task Handles_missing_client_null_reference()
    {
        var stubBackChannelLogoutClient = new StubBackChannelLogoutClient();
        Service = new DefaultSessionCoordinationService(
            new IdentityServerOptions(),
            new InMemoryPersistedGrantStore(),
            new InMemoryClientStore([]),
            stubBackChannelLogoutClient,
            new NullLogger<DefaultSessionCoordinationService>());

        await Service.ProcessExpirationAsync(new UserSession
        {
            ClientIds = ["not_found"],
            SessionId = "1",
            SubjectId = "1"
        }, _ct);

        stubBackChannelLogoutClient
            .SendLogoutsWasCalled
            .ShouldBeFalse();
    }
}
