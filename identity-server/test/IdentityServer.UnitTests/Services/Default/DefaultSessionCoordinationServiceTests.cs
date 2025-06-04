using System.Threading.Tasks;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Stores;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using UnitTests.Endpoints.EndSession;
using Xunit;

namespace UnitTests.Services.Default;

public class DefaultSessionCoordinationServiceTests
{
    public DefaultSessionCoordinationService Service;

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
        });
        
        stubBackChannelLogoutClient
            .SendLogoutsWasCalled
            .ShouldBeFalse();
    }
}