// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Linq;
using Duende.Bff.Tests.TestHosts;
using FluentAssertions;
using System.Net.Http;
using System.Security.Claims;
using System.Threading.Tasks;
using Xunit;
using System.Net;

namespace Duende.Bff.Tests.Endpoints.Management
{
    public class UserEndpointTests : BffIntegrationTestBase
    {
        [Fact]
        public async Task user_endpoint_for_authenticated_user_should_return_claims()
        {
            await BffHost.IssueSessionCookieAsync(
                new Claim("sub", "alice"), 
                new Claim("foo", "foo1"), 
                new Claim("foo", "foo2"));

            var data = await BffHost.CallUserEndpointAsync();

            data.Count.Should().Be(4);
            data.First(d => d.type == "sub").value.GetString().Should().Be("alice");

            var foos = data.Where(d => d.type == "foo");
            foos.Count().Should().Be(2);
            foos.First().value.GetString().Should().Be("foo1");
            foos.Skip(1).First().value.GetString().Should().Be("foo2");

            data.First(d => d.type == Constants.ClaimTypes.SessionExpiresIn).value.GetInt32().Should().BePositive();
        }
        
        [Fact]
        public async Task user_endpoint_for_authenticated_user_with_sid_should_return_claims_including_logout()
        {
            await BffHost.IssueSessionCookieAsync(
                new Claim("sub", "alice"),
                new Claim("sid", "123"));
        
            var data = await BffHost.CallUserEndpointAsync();

            data.Count.Should().Be(4);
            data.First(d => d.type == "sub").value.GetString().Should().Be("alice");
            data.First(d => d.type == "sid").value.GetString().Should().Be("123");
            data.First(d => d.type == Constants.ClaimTypes.LogoutUrl).value.GetString().Should().Be("/bff/logout?sid=123");
            data.First(d => d.type == Constants.ClaimTypes.SessionExpiresIn).value.GetInt32().Should().BePositive();
        }

        [Fact]
        public async Task user_endpoint_for_authenticated_user_without_csrf_header_should_fail()
        {
            await BffHost.IssueSessionCookieAsync(new Claim("sub", "alice"), new Claim("foo", "foo1"), new Claim("foo", "foo2"));

            var req = new HttpRequestMessage(HttpMethod.Get, BffHost.Url("/bff/user"));
            var response = await BffHost.BrowserClient.SendAsync(req);
            
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task user_endpoint_for_unauthenticated_user_should_fail()
        {
            var req = new HttpRequestMessage(HttpMethod.Get, BffHost.Url("/bff/user"));
            req.Headers.Add("x-csrf", "1");
            var response = await BffHost.BrowserClient.SendAsync(req);

            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task when_configured_user_endpoint_for_unauthenticated_user_should_return_200_and_null()
        {
            BffHost.BffOptions.AnonymousSessionResponse = AnonymousSessionResponse.Response200;

            var data = await BffHost.CallUserEndpointAsync();
            data.Should().BeNull();
        }
    }
}
