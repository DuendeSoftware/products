// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using Duende.Bff.Blazor.Client.Internals;

namespace Duende.Bff.Blazor.Client.UnitTests;

public class AntiforgeryHandlerTests
{
    [Fact]
    public async Task Adds_expected_header()
    {
        var sut = new TestAntiforgeryHandler()
        {
            InnerHandler = new NoOpHttpMessageHandler()
        };

        var request = new HttpRequestMessage();

        await sut.SendAsync(request, CancellationToken.None);

        request.Headers.ShouldContain(h => h.Key == "X-CSRF" && h.Value.Contains("1"));
    }
}

internal class TestAntiforgeryHandler : AntiforgeryHandler
{
    public new Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => base.SendAsync(request, cancellationToken);
}

public class NoOpHttpMessageHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
}
