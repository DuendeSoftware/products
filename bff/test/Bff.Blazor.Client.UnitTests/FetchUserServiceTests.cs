// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using System.Text.Json;
using Duende.Bff.Blazor.Client.Internals;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Duende.Bff.Blazor.Client.UnitTests;

public class FetchUserServiceTests
{
    private record ClaimRecord(string Type, object Value);

    [Fact]
    public async Task GetUserAsync_maps_claims_into_ClaimsPrincipal()
    {
        var claims = new List<ClaimRecord>
        {
            new("name", "example-user"),
            new("role", "admin"),
            new("foo", "bar")
        };
        var json = JsonSerializer.Serialize(claims);
        var factory = TestMocks.MockHttpClientFactory(json, HttpStatusCode.OK);
        var sut = new FetchUserService(factory, Substitute.For<ILogger<FetchUserService>>());

        var result = await sut.FetchUserAsync();

        result.IsInRole("admin").ShouldBeTrue();
        result.IsInRole("garbage").ShouldBeFalse();
        result.Identity.ShouldNotBeNull();
        result.Identity.Name.ShouldBe("example-user");
        result.FindFirst("foo").ShouldNotBeNull()
            .Value.ShouldBe("bar");
    }

    [Fact]
    public async Task GetUserAsync_returns_anonymous_when_http_request_fails()
    {
        var factory = TestMocks.MockHttpClientFactory("Internal Server Error", HttpStatusCode.InternalServerError);
        var sut = new FetchUserService(factory, Substitute.For<ILogger<FetchUserService>>());

        var errorResult = await sut.FetchUserAsync();
        errorResult.Identity?.IsAuthenticated.ShouldBeFalse();
    }
}

public class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly string _response;
    private readonly HttpStatusCode _statusCode;

    public string? RequestContent { get; private set; }

    public MockHttpMessageHandler(string response, HttpStatusCode statusCode)
    {
        _response = response;
        _statusCode = statusCode;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (request.Content != null) // Could be a GET-request without a body
        {
            RequestContent = await request.Content.ReadAsStringAsync();
        }
        return new HttpResponseMessage
        {
            StatusCode = _statusCode,
            Content = new StringContent(_response)
        };
    }
}
