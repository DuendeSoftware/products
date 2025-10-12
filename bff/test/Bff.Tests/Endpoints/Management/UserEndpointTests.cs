// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using System.Security.Claims;
using Duende.Bff.Tests.TestHosts;

namespace Duende.Bff.Tests.Endpoints.Management;

public class UserEndpointTests(ITestOutputHelper output) : BffIntegrationTestBase(output)
{
    [Fact]
    public async Task user_endpoint_for_authenticated_user_should_return_claims()
    {
        await BffHost.IssueSessionCookieAsync(
            new Claim("sub", "alice"),
            new Claim("foo", "foo1"),
            new Claim("foo", "foo2"));

        var data = await BffHost.CallUserEndpointAsync();

        data.Count.ShouldBe(5);
        data.First(d => d.Type == "sub").Value.GetString().ShouldBe("alice");

        var foos = data.Where(d => d.Type == "foo");
        foos.Count().ShouldBe(2);
        foos.First().Value.GetString().ShouldBe("foo1");
        foos.Skip(1).First().Value.GetString().ShouldBe("foo2");

        data.First(d => d.Type == Constants.ClaimTypes.SessionExpiresIn).Value.GetInt32().ShouldBePositive();
        data.First(d => d.Type == Constants.ClaimTypes.LogoutUrl).Value.GetString().ShouldBe("/bff/logout");
    }

    [Fact]
    public async Task user_endpoint_for_authenticated_user_with_sid_should_return_claims_including_logout()
    {
        await BffHost.IssueSessionCookieAsync(
            new Claim("sub", "alice"),
            new Claim("sid", "123"));

        var data = await BffHost.CallUserEndpointAsync();

        data.Count.ShouldBe(4);
        data.First(d => d.Type == "sub").Value.GetString().ShouldBe("alice");
        data.First(d => d.Type == "sid").Value.GetString().ShouldBe("123");
        data.First(d => d.Type == Constants.ClaimTypes.LogoutUrl).Value.GetString().ShouldBe("/bff/logout?sid=123");
        data.First(d => d.Type == Constants.ClaimTypes.SessionExpiresIn).Value.GetInt32().ShouldBePositive();
    }

    [Fact]
    public async Task user_endpoint_for_authenticated_user_without_csrf_header_should_fail()
    {
        await BffHost.IssueSessionCookieAsync(new Claim("sub", "alice"), new Claim("foo", "foo1"), new Claim("foo", "foo2"));

        var req = new HttpRequestMessage(HttpMethod.Get, BffHost.Url("/bff/user"));
        var response = await BffHost.BrowserClient.SendAsync(req);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task user_endpoint_for_unauthenticated_user_should_fail()
    {
        var req = new HttpRequestMessage(HttpMethod.Get, BffHost.Url("/bff/user"));
        req.Headers.Add("x-csrf", "1");
        var response = await BffHost.BrowserClient.SendAsync(req);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task when_configured_user_endpoint_for_unauthenticated_user_should_return_200_and_empty()
    {
        BffHost.BffOptions.AnonymousSessionResponse = AnonymousSessionResponse.Response200;

        var data = await BffHost.CallUserEndpointAsync();
        data.ShouldBeEmpty();
    }
}
