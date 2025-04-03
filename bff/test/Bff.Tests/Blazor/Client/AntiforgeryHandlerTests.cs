// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;

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

#pragma warning disable CS0618 // Type or member is obsolete
public class TestAntiforgeryHandler : AntiforgeryHandler
#pragma warning restore CS0618 // Type or member is obsolete
{
    public new Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return base.SendAsync(request, cancellationToken);
    }
}

public class NoOpHttpMessageHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
    }
}
