// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Net;
using Duende.IdentityServer.IntegrationTests.Common;

namespace Duende.IdentityServer.IntegrationTests.Endpoints.CheckSession;

public class CheckSessionTests
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;
    private readonly IdentityServerPipeline _mockPipeline = new IdentityServerPipeline();
    private const string Category = "Check session endpoint";

    public CheckSessionTests() => _mockPipeline.Initialize();

    [Fact]
    [Trait("Category", Category)]
    public async Task get_request_should_not_return_404()
    {
        var response = await _mockPipeline.BackChannelClient.GetAsync(IdentityServerPipeline.CheckSessionEndpoint, _ct);

        response.StatusCode.ShouldNotBe(HttpStatusCode.NotFound);
    }
}
