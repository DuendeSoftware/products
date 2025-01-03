// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Duende.Bff.Blazor.Client.UnitTests;

public static class TestMocks
{
    public static IHttpClientFactory MockHttpClientFactory(string response, HttpStatusCode status)
    {
        var httpClient = new HttpClient(new MockHttpMessageHandler(response, status))
        {
            // Just have to set something that looks reasonably like a URL so that the HttpClient's internal validation
            // doesn't blow up
            BaseAddress = new Uri("https://example.com") 
        };
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(BffClientAuthenticationStateProvider.HttpClientName).Returns(httpClient);
        return factory;
    }

    public static IOptions<BffBlazorOptions> MockOptions(BffBlazorOptions? opt = null)
    {
        var result = Substitute.For<IOptions<BffBlazorOptions>>();
        result.Value.Returns(opt ?? new BffBlazorOptions());
        return result;
    }
}