// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.Tests.TestHosts;
using FluentAssertions;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Duende.Bff.Tests.TestFramework;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Yarp.ReverseProxy.Transforms;
using Yarp.ReverseProxy.Transforms.Builder;

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

        [Fact]
        public async Task authenticated_call_to_route_with_token_metadata_should_not_forward_cookie_header_to_api()
        {
            await BffHost.BffLoginAsync("alice");

            var apiResult = await CallApiAsync(BffHost, "/api_user/test");

            apiResult.RequestHeaders.Keys.Should().NotContain(IsCookieHeader);
        }

        [Fact]
        public async Task authenticated_call_to_route_without_token_metadata_should_not_forward_cookie_header_to_api()
        {
            await BffHost.BffLoginAsync("alice");

            var apiResult = await CallApiAsync(BffHost, "/api_anon/test");

            apiResult.RequestHeaders.Keys.Should().NotContain(IsCookieHeader);
        }

        [Fact]
        public async Task when_opted_out_cookie_header_is_forwarded_to_api()
        {
            var bffHost = new YarpBffHost(IdentityServerHost, ApiHost, "spa");
            bffHost.OnConfigureServices += services =>
                services.Configure<BffOptions>(options => options.RemoveCookieHeaderFromYarpRequests = false);
            await bffHost.InitializeAsync();
            await bffHost.BffLoginAsync("alice");

            var apiResult = await CallApiAsync(bffHost, "/api_anon/test");

            apiResult.RequestHeaders.Keys.Should().Contain(IsCookieHeader);
        }

        [Fact]
        public async Task route_config_transform_setting_cookie_header_is_still_removed()
        {
            await BffHost.BffLoginAsync("alice");

            var apiResult = await CallApiAsync(BffHost, "/api_cookie_transform/test");

            apiResult.RequestHeaders.Keys.Should().NotContain(IsCookieHeader);
        }

        [Fact]
        public async Task transform_registered_after_add_bff_extensions_can_reintroduce_cookie_header()
        {
            var bffHost = new YarpBffHost(IdentityServerHost, ApiHost, "spa")
            {
                ConfigureReverseProxy = builder => builder.AddTransforms(context =>
                    context.AddRequestTransform(transformContext =>
                    {
                        transformContext.ProxyRequest.Headers.Add("Cookie", "from-later-transform=1");
                        return ValueTask.CompletedTask;
                    }))
            };
            await bffHost.InitializeAsync();
            await bffHost.BffLoginAsync("alice");

            var apiResult = await CallApiAsync(bffHost, "/api_anon/test");

            apiResult.RequestHeaders.Keys.Should().Contain(IsCookieHeader);
        }

        [Fact]
        public async Task yarp_route_should_remove_set_cookie_and_clear_site_data_from_api_response()
        {
            await BffHost.BffLoginAsync("alice");

            ApiHost.ResponseHeadersToReturn["Set-Cookie"] = "from-api=1; path=/";
            ApiHost.ResponseHeadersToReturn["Clear-Site-Data"] = "\"cookies\"";
            ApiHost.ResponseHeadersToReturn["x-from-api"] = "kept";

            var req = new HttpRequestMessage(HttpMethod.Get, BffHost.Url("/api_user/test"));
            req.Headers.Add("x-csrf", "1");
            var response = await BffHost.BrowserClient.SendAsync(req);

            response.IsSuccessStatusCode.Should().BeTrue();

            var setCookieValues = response.Headers.TryGetValues("Set-Cookie", out var values)
                ? values
                : Enumerable.Empty<string>();
            setCookieValues.Should().NotContain(v => v.StartsWith("from-api=", StringComparison.Ordinal));
            response.Headers.Contains("Clear-Site-Data").Should().BeFalse();
            response.Headers.GetValues("x-from-api").Should().BeEquivalentTo(new[] { "kept" });
        }

        [Fact]
        public async Task yarp_route_should_keep_set_cookie_and_clear_site_data_from_api_response_when_opted_out()
        {
            var bffHost = new YarpBffHost(IdentityServerHost, ApiHost, "spa");
            bffHost.OnConfigureServices += services =>
                services.Configure<BffOptions>(options => options.RemoveCookieHeadersFromProxiedResponses = false);
            await bffHost.InitializeAsync();
            await bffHost.BffLoginAsync("alice");

            ApiHost.ResponseHeadersToReturn["Set-Cookie"] = "from-api=1; path=/";
            ApiHost.ResponseHeadersToReturn["Clear-Site-Data"] = "\"cookies\"";

            var req = new HttpRequestMessage(HttpMethod.Get, bffHost.Url("/api_user/test"));
            req.Headers.Add("x-csrf", "1");
            var response = await bffHost.BrowserClient.SendAsync(req);

            response.IsSuccessStatusCode.Should().BeTrue();

            var setCookieValues = response.Headers.TryGetValues("Set-Cookie", out var values)
                ? values
                : Enumerable.Empty<string>();
            setCookieValues.Should().Contain(v => v.StartsWith("from-api=", StringComparison.Ordinal));
            response.Headers.Contains("Clear-Site-Data").Should().BeTrue();
        }

        // Writes a BFF-side Set-Cookie equal to the API's cookie value, via an ITransformProvider
        // inserted ahead of the BFF's own cookie-removal transform provider. This exercises the
        // `HeadersCopied` guard in ApiCookieResponseHeaderRemoveTransform: on the
        // api_response_headers_copy_disabled route, nothing from the API was copied, so the removal
        // transform must leave this BFF-written cookie alone.
        private sealed class AddBffCookieTransformProvider : ITransformProvider
        {
            public void ValidateRoute(TransformRouteValidationContext context)
            {
            }

            public void ValidateCluster(TransformClusterValidationContext context)
            {
            }

            public void Apply(TransformBuilderContext context) =>
                context.AddResponseTransform(responseContext =>
                {
                    responseContext.HttpContext.Response.Headers["Set-Cookie"] = "affinity=1; path=/";
                    return ValueTask.CompletedTask;
                });
        }

        [Fact]
        public async Task when_response_header_copy_is_disabled_a_bff_cookie_equal_to_the_api_cookie_reaches_the_browser()
        {
            var bffHost = new YarpBffHost(IdentityServerHost, ApiHost, "spa");
            bffHost.OnConfigureServices += services =>
                services.Insert(0, ServiceDescriptor.Singleton<ITransformProvider, AddBffCookieTransformProvider>());
            await bffHost.InitializeAsync();
            await bffHost.BffLoginAsync("alice");

            ApiHost.ResponseHeadersToReturn["Set-Cookie"] = "affinity=1; path=/";

            var req = new HttpRequestMessage(HttpMethod.Get, bffHost.Url("/api_response_headers_copy_disabled/test"));
            req.Headers.Add("x-csrf", "1");
            var response = await bffHost.BrowserClient.SendAsync(req);

            response.IsSuccessStatusCode.Should().BeTrue();
            response.Headers.GetValues("Set-Cookie").Should().BeEquivalentTo(new[] { "affinity=1; path=/" });
        }

        [Fact]
        public async Task when_set_cookie_is_in_the_response_header_allow_list_the_api_cookie_is_still_removed()
        {
            await BffHost.BffLoginAsync("alice");

            ApiHost.ResponseHeadersToReturn["Set-Cookie"] = "from-api=1; path=/";
            ApiHost.ResponseHeadersToReturn["x-from-api"] = "kept";

            var req = new HttpRequestMessage(HttpMethod.Get, BffHost.Url("/api_response_headers_allowed/test"));
            req.Headers.Add("x-csrf", "1");
            var response = await BffHost.BrowserClient.SendAsync(req);

            response.IsSuccessStatusCode.Should().BeTrue();
            response.Headers.Contains("Set-Cookie").Should().BeFalse();
            response.Headers.GetValues("x-from-api").Should().BeEquivalentTo(new[] { "kept" });
        }

        private static readonly System.Linq.Expressions.Expression<Func<string, bool>> IsCookieHeader =
            key => string.Equals(key, "Cookie", StringComparison.OrdinalIgnoreCase);

        private static async Task<ApiResponse> CallApiAsync(YarpBffHost host, string path)
        {
            var req = new HttpRequestMessage(HttpMethod.Get, host.Url(path));
            req.Headers.Add("x-csrf", "1");
            var response = await host.BrowserClient.SendAsync(req);

            response.IsSuccessStatusCode.Should().BeTrue();
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<ApiResponse>(json);
        }
    }
}
