// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using Duende.Bff.Blazor.Client.Internals;

namespace Duende.Bff.Blazor.Client.UnitTests;

public class AntiForgeryHandlerTests
{
    [Fact]
    public async Task Adds_expected_header()
    {
        var sut = new AntiForgeryHandler()
        {
            InnerHandler = new NoOpHttpMessageHandler()
        };

        var request = new HttpRequestMessage()
        {
            RequestUri = new Uri("https://won-t")
        };

        var client = new HttpClient(sut);

        await client.SendAsync(request, Ct.None);

        request.Headers.ShouldContain(h => h.Key == "X-CSRF" && h.Value.Contains("1"));
    }
}

public class NoOpHttpMessageHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, Ct ct) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
}
