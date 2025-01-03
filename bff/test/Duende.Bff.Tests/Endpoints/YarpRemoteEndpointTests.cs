// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.Tests.TestHosts;
using FluentAssertions;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Duende.Bff.Tests.TestFramework;
using Xunit;

namespace Duende.Bff.Tests.Endpoints
{
    public class YarpRemoteEndpointTests : YarpBffIntegrationTestBase
    {
        [Fact]
        public async Task anonymous_call_with_no_csrf_header_to_no_token_requirement_no_csrf_route_should_succeed()
        {
            var req = new HttpRequestMessage(HttpMethod.Get, BffHost.Url("/api_anon_no_csrf/test"));
            var response = await BffHost.BrowserClient.SendAsync(req);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }
        
        [Fact]
        public async Task anonymous_call_with_no_csrf_header_to_csrf_route_should_fail()
        {
            var req = new HttpRequestMessage(HttpMethod.Get, BffHost.Url("/api_anon/test"));
            var response = await BffHost.BrowserClient.SendAsync(req);

            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }
        
        [Fact]
        public async Task anonymous_call_to_no_token_requirement_route_should_succeed()
        {
            var req = new HttpRequestMessage(HttpMethod.Get, BffHost.Url("/api_anon/test"));
            req.Headers.Add("x-csrf", "1");
            var response = await BffHost.BrowserClient.SendAsync(req);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }
        
        [Fact]
        public async Task anonymous_call_to_user_token_requirement_route_should_fail()
        {
            var req = new HttpRequestMessage(HttpMethod.Get, BffHost.Url("/api_user/test"));
            req.Headers.Add("x-csrf", "1");
            var response = await BffHost.BrowserClient.SendAsync(req);

            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task anonymous_call_to_optional_user_token_route_should_succeed()
        {
            var req = new HttpRequestMessage(HttpMethod.Get, BffHost.Url("/api_optional_user/test"));
            req.Headers.Add("x-csrf", "1");
            var response = await BffHost.BrowserClient.SendAsync(req);

            response.IsSuccessStatusCode.Should().BeTrue();
            response.Content.Headers.ContentType.MediaType.Should().Be("application/json");
            var json = await response.Content.ReadAsStringAsync();
            var apiResult = JsonSerializer.Deserialize<ApiResponse>(json);
            apiResult.Method.Should().Be("GET");
            apiResult.Path.Should().Be("/api_optional_user/test");
            apiResult.Sub.Should().BeNull();
            apiResult.ClientId.Should().BeNull();
        }

        [Theory]
        [InlineData("/api_user/test")]
        [InlineData("/api_optional_user/test")]
        public async Task authenticated_GET_should_forward_user_to_api(string route)
        {
            await BffHost.BffLoginAsync("alice");
        
            var req = new HttpRequestMessage(HttpMethod.Get, BffHost.Url(route));
            req.Headers.Add("x-csrf", "1");
            var response = await BffHost.BrowserClient.SendAsync(req);
        
            response.IsSuccessStatusCode.Should().BeTrue();
            response.Content.Headers.ContentType.MediaType.Should().Be("application/json");
            var json = await response.Content.ReadAsStringAsync();
            var apiResult = JsonSerializer.Deserialize<ApiResponse>(json);
            apiResult.Method.Should().Be("GET");
            apiResult.Path.Should().Be(route);
            apiResult.Sub.Should().Be("alice");
            apiResult.ClientId.Should().Be("spa");
        }
        
        [Theory]
        [InlineData("/api_user/test")]
        [InlineData("/api_optional_user/test")]
        public async Task authenticated_PUT_should_forward_user_to_api(string route)
        {
            await BffHost.BffLoginAsync("alice");
        
            var req = new HttpRequestMessage(HttpMethod.Put, BffHost.Url(route));
            req.Headers.Add("x-csrf", "1");
            var response = await BffHost.BrowserClient.SendAsync(req);
        
            response.IsSuccessStatusCode.Should().BeTrue();
            response.Content.Headers.ContentType.MediaType.Should().Be("application/json");
            var json = await response.Content.ReadAsStringAsync();
            var apiResult = JsonSerializer.Deserialize<ApiResponse>(json);
            apiResult.Method.Should().Be("PUT");
            apiResult.Path.Should().Be(route);
            apiResult.Sub.Should().Be("alice");
            apiResult.ClientId.Should().Be("spa");
        }

        [Theory]
        [InlineData("/api_user/test")]
        [InlineData("/api_optional_user/test")]
        public async Task authenticated_POST_should_forward_user_to_api(string route)
        {
            await BffHost.BffLoginAsync("alice");
        
            var req = new HttpRequestMessage(HttpMethod.Post, BffHost.Url(route));
            req.Headers.Add("x-csrf", "1");
            var response = await BffHost.BrowserClient.SendAsync(req);
        
            response.IsSuccessStatusCode.Should().BeTrue();
            response.Content.Headers.ContentType.MediaType.Should().Be("application/json");
            var json = await response.Content.ReadAsStringAsync();
            var apiResult = JsonSerializer.Deserialize<ApiResponse>(json);
            apiResult.Method.Should().Be("POST");
            apiResult.Path.Should().Be(route);
            apiResult.Sub.Should().Be("alice");
            apiResult.ClientId.Should().Be("spa");
        }
        
        [Fact]
        public async Task call_to_client_token_route_should_forward_client_token_to_api()
        {
            await BffHost.BffLoginAsync("alice");
        
            var req = new HttpRequestMessage(HttpMethod.Get, BffHost.Url("/api_client/test"));
            req.Headers.Add("x-csrf", "1");
            var response = await BffHost.BrowserClient.SendAsync(req);
        
            response.IsSuccessStatusCode.Should().BeTrue();
            response.Content.Headers.ContentType.MediaType.Should().Be("application/json");
            var json = await response.Content.ReadAsStringAsync();
            var apiResult = JsonSerializer.Deserialize<ApiResponse>(json);
            apiResult.Method.Should().Be("GET");
            apiResult.Path.Should().Be("/api_client/test");
            apiResult.Sub.Should().BeNull();
            apiResult.ClientId.Should().Be("spa");
        }
        
        [Fact]
        public async Task call_to_user_or_client_token_route_should_forward_user_or_client_token_to_api()
        {
            {
                var req = new HttpRequestMessage(HttpMethod.Get, BffHost.Url("/api_user_or_client/test"));
                req.Headers.Add("x-csrf", "1");
                var response = await BffHost.BrowserClient.SendAsync(req);
        
                response.IsSuccessStatusCode.Should().BeTrue();
                response.Content.Headers.ContentType.MediaType.Should().Be("application/json");
                var json = await response.Content.ReadAsStringAsync();
                var apiResult = JsonSerializer.Deserialize<ApiResponse>(json);
                apiResult.Method.Should().Be("GET");
                apiResult.Path.Should().Be("/api_user_or_client/test");
                apiResult.Sub.Should().BeNull();
                apiResult.ClientId.Should().Be("spa");
            }
        
            {
                await BffHost.BffLoginAsync("alice");
        
                var req = new HttpRequestMessage(HttpMethod.Get, BffHost.Url("/api_user_or_client/test"));
                req.Headers.Add("x-csrf", "1");
                var response = await BffHost.BrowserClient.SendAsync(req);
        
                response.IsSuccessStatusCode.Should().BeTrue();
                response.Content.Headers.ContentType.MediaType.Should().Be("application/json");
                var json = await response.Content.ReadAsStringAsync();
                var apiResult = JsonSerializer.Deserialize<ApiResponse>(json);
                apiResult.Method.Should().Be("GET");
                apiResult.Path.Should().Be("/api_user_or_client/test");
                apiResult.Sub.Should().Be("alice");
                apiResult.ClientId.Should().Be("spa");
            }
        }
        
        [Fact]
        public async Task response_status_401_from_remote_endpoint_should_return_401_from_bff()
        {
            await BffHost.BffLoginAsync("alice");
            ApiHost.ApiStatusCodeToReturn = 401;
        
            var req = new HttpRequestMessage(HttpMethod.Get, BffHost.Url("/api_user/test"));
            req.Headers.Add("x-csrf", "1");
            var response = await BffHost.BrowserClient.SendAsync(req);
        
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }
        
        [Fact]
        public async Task response_status_403_from_remote_endpoint_should_return_403_from_bff()
        {
            await BffHost.BffLoginAsync("alice");
            ApiHost.ApiStatusCodeToReturn = 403;
        
            var req = new HttpRequestMessage(HttpMethod.Get, BffHost.Url("/api_user/test"));
            req.Headers.Add("x-csrf", "1");
            var response = await BffHost.BrowserClient.SendAsync(req);
        
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        [Fact]
        public async Task invalid_configuration_of_routes_should_return_500()
        {
            var req = new HttpRequestMessage(HttpMethod.Get, BffHost.Url("/api_invalid/test"));
            var response = await BffHost.BrowserClient.SendAsync(req);
        
            response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        }
    }
}
