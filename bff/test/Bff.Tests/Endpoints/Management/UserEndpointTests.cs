// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using System.Security.Claims;
using Duende.Bff.Configuration;
using Duende.Bff.Tests.TestInfra;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Xunit.Abstractions;

namespace Duende.Bff.Tests.Endpoints.Management;

public class UserEndpointTests : BffTestBase, IAsyncLifetime
{
    private List<Claim> ClaimsToAdd = [];

    public UserEndpointTests(ITestOutputHelper output) : base(output)
    {
        IdentityServer.AddClient(The.ClientId, Bff.Url());

        Bff.OnConfigureBff += bff => bff
            .WithDefaultOpenIdConnectOptions(opt =>
            {
                opt.Events.OnTokenValidated = context =>
                {
                    var identity = (ClaimsIdentity)context.Principal!.Identity!;
                    foreach (var claim in ClaimsToAdd)
                    {
                        identity.AddClaim(claim);
                    }

                    return Task.CompletedTask;
                }; The.DefaultOpenIdConnectConfiguration(opt);
            });

        Bff.OnConfigureEndpoints += endpoints =>
        {
            // Setup a login endpoint that allows you to simulate signing in as a specific
            // user in the BFF. 
            endpoints.MapGet("/__signin", async ctx =>
            {
                var props = new AuthenticationProperties();
                await ctx.SignInAsync(UserToSignIn!, props);

                ctx.Response.StatusCode = 204;
            });
        };
    }

    public ClaimsPrincipal? UserToSignIn { get; set; }

    [Fact]
    public async Task user_endpoint_for_authenticated_user_should_return_claims()
    {
        ClaimsToAdd.Add(new Claim("foo", "foo1"));
        ClaimsToAdd.Add(new Claim("foo", "foo2"));
        await Bff.BrowserClient.Login();

        var data = await Bff.BrowserClient.CallUserEndpointAsync();

        data.First(d => d.Type == "sub").Value.GetString().ShouldBe(The.Sub);

        var foos = data.Where(d => d.Type == "foo");
        foos.Count().ShouldBe(2);
        foos.First().Value.GetString().ShouldBe("foo1");
        foos.Skip(1).First().Value.GetString().ShouldBe("foo2");

        data.First(d => d.Type == Constants.ClaimTypes.SessionExpiresIn).Value.GetInt32().ShouldBePositive();
        data.First(d => d.Type == Constants.ClaimTypes.LogoutUrl).Value.GetString().ShouldStartWith("/bff/logout?sid=");
    }

    [Fact]
    public async Task user_endpoint_for_authenticated_user_with_sid_should_return_claims_including_logout()
    {
        UserToSignIn = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim("sub", "alice"),
            new Claim("sid", "123"),
        ], "test", "name", "role"));

        await Bff.BrowserClient.GetAsync("/__signin");

        var data = await Bff.BrowserClient.CallUserEndpointAsync();

        data.Count.ShouldBe(4);
        data.First(d => d.Type == "sub").Value.GetString().ShouldBe("alice");
        data.First(d => d.Type == "sid").Value.GetString().ShouldBe("123");
        data.First(d => d.Type == Constants.ClaimTypes.LogoutUrl).Value.GetString().ShouldBe("/bff/logout?sid=123");
        data.First(d => d.Type == Constants.ClaimTypes.SessionExpiresIn).Value.GetInt32().ShouldBePositive();
    }

    [Fact]
    public async Task user_endpoint_for_authenticated_user_without_csrf_header_should_fail()
    {
        await Bff.BrowserClient.IssueSessionCookieAsync(new Claim("sub", "alice"), new Claim("foo", "foo1"), new Claim("foo", "foo2"));

        var req = new HttpRequestMessage(HttpMethod.Get, Bff.Url("/bff/user"));
        var response = await Bff.BrowserClient.SendAsync(req);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task user_endpoint_for_unauthenticated_user_should_fail()
    {
        var req = new HttpRequestMessage(HttpMethod.Get, Bff.Url("/bff/user"));
        req.Headers.Add("x-csrf", "1");
        var response = await Bff.BrowserClient.SendAsync(req);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task when_configured_user_endpoint_for_unauthenticated_user_should_return_200_and_empty()
    {

        var options = Bff.Resolve<IOptions<BffOptions>>();

        options.Value.AnonymousSessionResponse = AnonymousSessionResponse.Response200;

        var data = await Bff.BrowserClient.CallUserEndpointAsync();
        data.ShouldBeEmpty();
    }
}
