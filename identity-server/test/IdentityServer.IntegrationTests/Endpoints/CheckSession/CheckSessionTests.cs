// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using FluentAssertions;
using IntegrationTests.Common;
using System.Net;
using System.Threading.Tasks;
using Xunit;

namespace IntegrationTests.Endpoints.CheckSession;

public class CheckSessionTests
{
    private const string Category = "Check session endpoint";

    private IdentityServerPipeline _mockPipeline = new IdentityServerPipeline();

    public CheckSessionTests()
    {
        _mockPipeline.Initialize();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task get_request_should_not_return_404()
    {
        var response = await _mockPipeline.BackChannelClient.GetAsync(IdentityServerPipeline.CheckSessionEndpoint);

        response.StatusCode.Should().NotBe(HttpStatusCode.NotFound);
    }
}
