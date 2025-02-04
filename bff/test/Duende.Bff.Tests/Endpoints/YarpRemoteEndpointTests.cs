// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.Tests.TestHosts;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace Duende.Bff.Tests.Endpoints
{
    public class YarpRemoteEndpointTests(ITestOutputHelper output) : YarpBffIntegrationTestBase(output)
    {
        [Fact]
        public async Task anonymous_call_with_no_csrf_header_to_no_token_requirement_no_csrf_route_should_succeed()
        {
            await BffHost.BrowserClient.CallBffHostApi(
                url: BffHost.Url("/api_anon_no_csrf/test"),
                expectedStatusCode: HttpStatusCode.OK
            );
        }

        [Fact]
        public async Task anonymous_call_with_no_csrf_header_to_csrf_route_should_fail()
        {
            var req = new HttpRequestMessage(HttpMethod.Get, BffHost.Url("/api_anon/test"));
            var response = await BffHost.BrowserClient.SendAsync(req);

            response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }


        [Fact]
        public async Task anonymous_call_to_no_token_requirement_route_should_succeed()
        {
            await BffHost.BrowserClient.CallBffHostApi(
                url: BffHost.Url("/api_anon/test"),
                expectedStatusCode: HttpStatusCode.OK
            );
        }

        [Fact]
        public async Task anonymous_call_to_user_token_requirement_route_should_fail()
        {
            await BffHost.BrowserClient.CallBffHostApi(
                url: BffHost.Url("/api_user/test"),
                expectedStatusCode: HttpStatusCode.Unauthorized
            );
        }

        [Fact]
        public async Task anonymous_call_to_optional_user_token_route_should_succeed()
        {
            var apiResult = await BffHost.BrowserClient.CallBffHostApi(
                url: BffHost.Url("/api_optional_user/test")
            );

            apiResult.Method.ShouldBe("GET");
            apiResult.Path.ShouldBe("/api_optional_user/test");
            apiResult.Sub.ShouldBeNull();
            apiResult.ClientId.ShouldBeNull();
        }

        [Theory]
        [InlineData("/api_user/test")]
        [InlineData("/api_optional_user/test")]
        public async Task authenticated_GET_should_forward_user_to_api(string route)
        {
            await BffHost.BffLoginAsync("alice");

            var apiResult = await BffHost.BrowserClient.CallBffHostApi(
                url: BffHost.Url(route)
            );

            apiResult.Method.ShouldBe("GET");
            apiResult.Path.ShouldBe(route);
            apiResult.Sub.ShouldBe("alice");
            apiResult.ClientId.ShouldBe("spa");
        }

        [Theory]
        [InlineData("/api_user/test")]
        [InlineData("/api_optional_user/test")]
        public async Task authenticated_PUT_should_forward_user_to_api(string route)
        {
            await BffHost.BffLoginAsync("alice");

            var apiResult = await BffHost.BrowserClient.CallBffHostApi(
                url: BffHost.Url(route),
                method: HttpMethod.Put
            );

            apiResult.Method.ShouldBe("PUT");
            apiResult.Path.ShouldBe(route);
            apiResult.Sub.ShouldBe("alice");
            apiResult.ClientId.ShouldBe("spa");
        }

        [Theory]
        [InlineData("/api_user/test")]
        [InlineData("/api_optional_user/test")]
        public async Task authenticated_POST_should_forward_user_to_api(string route)
        {
            await BffHost.BffLoginAsync("alice");

            var apiResult = await BffHost.BrowserClient.CallBffHostApi(
                url: BffHost.Url(route),
                method: HttpMethod.Post
            );

            apiResult.Method.ShouldBe("POST");
            apiResult.Path.ShouldBe(route);
            apiResult.Sub.ShouldBe("alice");
            apiResult.ClientId.ShouldBe("spa");
        }

        [Fact]
        public async Task call_to_client_token_route_should_forward_client_token_to_api()
        {
            await BffHost.BffLoginAsync("alice");

            var apiResult = await BffHost.BrowserClient.CallBffHostApi(
                url: BffHost.Url("/api_client/test")
            );

            apiResult.Method.ShouldBe("GET");
            apiResult.Path.ShouldBe("/api_client/test");
            apiResult.Sub.ShouldBeNull();
            apiResult.ClientId.ShouldBe("spa");
        }

        [Fact]
        public async Task call_to_user_or_client_token_route_should_forward_user_or_client_token_to_api()
        {
            {
                var apiResult = await BffHost.BrowserClient.CallBffHostApi(
                    url: BffHost.Url("/api_user_or_client/test")
                );

                apiResult.Method.ShouldBe("GET");
                apiResult.Path.ShouldBe("/api_user_or_client/test");
                apiResult.Sub.ShouldBeNull();
                apiResult.ClientId.ShouldBe("spa");
            }

            {
                await BffHost.BffLoginAsync("alice");

                var apiResult = await BffHost.BrowserClient.CallBffHostApi(
                    url: BffHost.Url("/api_user_or_client/test")
                );

                apiResult.Method.ShouldBe("GET");
                apiResult.Path.ShouldBe("/api_user_or_client/test");
                apiResult.Sub.ShouldBe("alice");
                apiResult.ClientId.ShouldBe("spa");
            }
        }

        [Fact]
        public async Task response_status_401_from_remote_endpoint_should_return_401_from_bff()
        {
            await BffHost.BffLoginAsync("alice");
            ApiHost.ApiStatusCodeToReturn = 401;

            var response = await BffHost.BrowserClient.CallBffHostApi(
                url: BffHost.Url("/api_user/test"),
                expectedStatusCode: HttpStatusCode.Unauthorized
            );
        }

        [Fact]
        public async Task response_status_403_from_remote_endpoint_should_return_403_from_bff()
        {
            await BffHost.BffLoginAsync("alice");
            ApiHost.ApiStatusCodeToReturn = 403;

            var response = await BffHost.BrowserClient.CallBffHostApi(
                url: BffHost.Url("/api_user/test"),
                expectedStatusCode: HttpStatusCode.Forbidden
            );
        }

        [Fact]
        public async Task invalid_configuration_of_routes_should_return_500()
        {
            var response = await BffHost.BrowserClient.CallBffHostApi(
                url: BffHost.Url("/api_invalid/test"),
                expectedStatusCode: HttpStatusCode.InternalServerError
            );
        }
    }
}