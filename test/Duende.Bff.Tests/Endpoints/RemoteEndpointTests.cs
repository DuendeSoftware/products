// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.Tests.TestFramework;
using Duende.Bff.Tests.TestHosts;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace Duende.Bff.Tests.Endpoints
{
    public class RemoteEndpointTests : BffIntegrationTestBase
    {
        [Fact]
        public async Task unauthenticated_calls_to_remote_endpoint_should_return_401()
        {
            var req = new HttpRequestMessage(HttpMethod.Get, BffHost.Url("/api_user/test"));
            req.Headers.Add("x-csrf", "1");
            var response = await BffHost.BrowserClient.SendAsync(req);

            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task calls_to_remote_endpoint_should_forward_user_to_api()
        {
            await BffHost.BffLoginAsync("alice");

            var req = new HttpRequestMessage(HttpMethod.Get, BffHost.Url("/api_user/test"));
            req.Headers.Add("x-csrf", "1");
            var response = await BffHost.BrowserClient.SendAsync(req);

            response.IsSuccessStatusCode.Should().BeTrue();
            response.Content.Headers.ContentType.MediaType.Should().Be("application/json");
            var json = await response.Content.ReadAsStringAsync();
            var apiResult = JsonSerializer.Deserialize<ApiResponse>(json);
            apiResult.Method.Should().Be("GET");
            apiResult.Path.Should().Be("/test");
            apiResult.Sub.Should().Be("alice");
            apiResult.ClientId.Should().Be("spa");
        }

        [Fact]
        public async Task calls_to_remote_endpoint_with_useraccesstokenparameters_having_stored_named_token_should_forward_user_to_api()
        {
            var loginResponse = await BffHostWithNamedTokens.BffLoginAsync("alice");

            var req = new HttpRequestMessage(HttpMethod.Get, BffHostWithNamedTokens.Url("/api_user_with_useraccesstokenparameters_having_stored_named_token/test"));
            req.Headers.Add("x-csrf", "1");
            var response = await BffHostWithNamedTokens.BrowserClient.SendAsync(req);

            response.IsSuccessStatusCode.Should().BeTrue();
            response.Content.Headers.ContentType.MediaType.Should().Be("application/json");
            var json = await response.Content.ReadAsStringAsync();
            var apiResult = JsonSerializer.Deserialize<ApiResponse>(json);
            apiResult.Method.Should().Be("GET");
            apiResult.Path.Should().Be("/test");
            apiResult.Sub.Should().Be("alice");
            apiResult.ClientId.Should().Be("spa");
        }

        [Fact]
        public async Task calls_to_remote_endpoint_with_useraccesstokenparameters_having_not_stored_corresponding_named_token_finds_no_matching_token_should_fail()
        {
            var loginResponse = await BffHostWithNamedTokens.BffLoginAsync("alice");

            var req = new HttpRequestMessage(HttpMethod.Get, BffHostWithNamedTokens.Url("/api_user_with_useraccesstokenparameters_having_not_stored_named_token/test"));
            req.Headers.Add("x-csrf", "1");

            var response = await BffHostWithNamedTokens.BrowserClient.SendAsync(req);
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task put_to_remote_endpoint_should_forward_user_to_api()
        {
            await BffHost.BffLoginAsync("alice");

            var req = new HttpRequestMessage(HttpMethod.Put, BffHost.Url("/api_user/test"));
            req.Headers.Add("x-csrf", "1");
            req.Content = new StringContent(JsonSerializer.Serialize(new TestPayload("hello test api")), Encoding.UTF8, "application/json");
            var response = await BffHost.BrowserClient.SendAsync(req);

            response.IsSuccessStatusCode.Should().BeTrue();
            response.Content.Headers.ContentType.MediaType.Should().Be("application/json");
            var json = await response.Content.ReadAsStringAsync();
            var apiResult = JsonSerializer.Deserialize<ApiResponse>(json);
            apiResult.Method.Should().Be("PUT");
            apiResult.Path.Should().Be("/test");
            apiResult.Sub.Should().Be("alice");
            apiResult.ClientId.Should().Be("spa");
            var body = JsonSerializer.Deserialize<TestPayload>(apiResult.Body);
            body.message.Should().Be("hello test api");
        }
        
        [Fact]
        public async Task post_to_remote_endpoint_should_forward_user_to_api()
        {
            await BffHost.BffLoginAsync("alice");

            var req = new HttpRequestMessage(HttpMethod.Post, BffHost.Url("/api_user/test"));
            req.Headers.Add("x-csrf", "1");
            req.Content = new StringContent(JsonSerializer.Serialize(new TestPayload("hello test api")), Encoding.UTF8, "application/json");
            var response = await BffHost.BrowserClient.SendAsync(req);

            response.IsSuccessStatusCode.Should().BeTrue();
            response.Content.Headers.ContentType.MediaType.Should().Be("application/json");
            var json = await response.Content.ReadAsStringAsync();
            var apiResult = JsonSerializer.Deserialize<ApiResponse>(json);
            apiResult.Method.Should().Be("POST");
            apiResult.Path.Should().Be("/test");
            apiResult.Sub.Should().Be("alice");
            apiResult.ClientId.Should().Be("spa");
            var body = JsonSerializer.Deserialize<TestPayload>(apiResult.Body);
            body.message.Should().Be("hello test api");
        }



        [Fact]
        public async Task calls_to_remote_endpoint_should_forward_user_or_anonymous_to_api()
        {
            {
                var req = new HttpRequestMessage(HttpMethod.Get, BffHost.Url("/api_user_or_anon/test"));
                req.Headers.Add("x-csrf", "1");
                var response = await BffHost.BrowserClient.SendAsync(req);

                response.IsSuccessStatusCode.Should().BeTrue();
                response.Content.Headers.ContentType.MediaType.Should().Be("application/json");
                var json = await response.Content.ReadAsStringAsync();
                var apiResult = JsonSerializer.Deserialize<ApiResponse>(json);
                apiResult.Method.Should().Be("GET");
                apiResult.Path.Should().Be("/test");
                apiResult.Sub.Should().BeNull();
                apiResult.ClientId.Should().BeNull();
            }

            {
                await BffHost.BffLoginAsync("alice");

                var req = new HttpRequestMessage(HttpMethod.Get, BffHost.Url("/api_user_or_anon/test"));
                req.Headers.Add("x-csrf", "1");
                var response = await BffHost.BrowserClient.SendAsync(req);

                response.IsSuccessStatusCode.Should().BeTrue();
                response.Content.Headers.ContentType.MediaType.Should().Be("application/json");
                var json = await response.Content.ReadAsStringAsync();
                var apiResult = JsonSerializer.Deserialize<ApiResponse>(json);
                apiResult.Method.Should().Be("GET");
                apiResult.Path.Should().Be("/test");
                apiResult.Sub.Should().Be("alice");
                apiResult.ClientId.Should().Be("spa");
            }
        }

        [Fact]
        public async Task calls_to_remote_endpoint_should_forward_client_token_to_api()
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
            apiResult.Path.Should().Be("/test");
            apiResult.Sub.Should().BeNull();
            apiResult.ClientId.Should().Be("spa");
        }

        [Fact]
        public async Task calls_to_remote_endpoint_should_fail_when_token_retrieval_fails()
        {
            await BffHost.BffLoginAsync("alice");

            var req = new HttpRequestMessage(HttpMethod.Get, BffHost.Url("/api_with_access_token_retrieval_that_fails"));
            req.Headers.Add("x-csrf", "1");
            var response = await BffHost.BrowserClient.SendAsync(req);

            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task calls_to_remote_endpoint_should_send_token_from_token_retriever_when_token_retrieval_succeeds()
        {
            await BffHost.BffLoginAsync("alice");

            var req = new HttpRequestMessage(HttpMethod.Get, BffHost.Url("/api_with_access_token_retriever"));
            req.Headers.Add("x-csrf", "1");
            var response = await BffHost.BrowserClient.SendAsync(req);

            var json = await response.Content.ReadAsStringAsync();
            var apiResult = JsonSerializer.Deserialize<ApiResponse>(json);
            apiResult.Sub.Should().Be("123");
            apiResult.ClientId.Should().Be("fake-client");
        }

        [Fact]
        public async Task calls_to_remote_endpoint_should_forward_user_or_client_to_api()
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
                apiResult.Path.Should().Be("/test");
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
                apiResult.Path.Should().Be("/test");
                apiResult.Sub.Should().Be("alice");
                apiResult.ClientId.Should().Be("spa");
            }
        }

        [Fact]
        public async Task calls_to_remote_endpoint_with_anon_should_be_anon()
        {
            {
                var req = new HttpRequestMessage(HttpMethod.Get, BffHost.Url("/api_anon_only/test"));
                req.Headers.Add("x-csrf", "1");
                var response = await BffHost.BrowserClient.SendAsync(req);

                response.IsSuccessStatusCode.Should().BeTrue();
                response.Content.Headers.ContentType.MediaType.Should().Be("application/json");
                var json = await response.Content.ReadAsStringAsync();
                var apiResult = JsonSerializer.Deserialize<ApiResponse>(json);
                apiResult.Method.Should().Be("GET");
                apiResult.Path.Should().Be("/test");
                apiResult.Sub.Should().BeNull();
                apiResult.ClientId.Should().BeNull();
            }

            {
                await BffHost.BffLoginAsync("alice");

                var req = new HttpRequestMessage(HttpMethod.Get, BffHost.Url("/api_anon_only/test"));
                req.Headers.Add("x-csrf", "1");
                var response = await BffHost.BrowserClient.SendAsync(req);

                response.IsSuccessStatusCode.Should().BeTrue();
                response.Content.Headers.ContentType.MediaType.Should().Be("application/json");
                var json = await response.Content.ReadAsStringAsync();
                var apiResult = JsonSerializer.Deserialize<ApiResponse>(json);
                apiResult.Method.Should().Be("GET");
                apiResult.Path.Should().Be("/test");
                apiResult.Sub.Should().BeNull();
                apiResult.ClientId.Should().BeNull();
            }
        }


        [Fact]
        public async Task calls_to_remote_endpoint_expecting_token_but_without_token_should_fail()
        {
            var client = IdentityServerHost.Clients.Single(x => x.ClientId == "spa");
            client.Enabled = false;

            {
                var req = new HttpRequestMessage(HttpMethod.Get, BffHost.Url("/api_user_or_client/test"));
                req.Headers.Add("x-csrf", "1");
                var response = await BffHost.BrowserClient.SendAsync(req);

                response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            }

            {
                var req = new HttpRequestMessage(HttpMethod.Get, BffHost.Url("/api_client/test"));
                req.Headers.Add("x-csrf", "1");
                var response = await BffHost.BrowserClient.SendAsync(req);

                response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
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
        public async Task calls_to_remote_endpoint_should_require_csrf()
        {
            var req = new HttpRequestMessage(HttpMethod.Get, BffHost.Url("/api_user_or_client/test"));
            var response = await BffHost.BrowserClient.SendAsync(req);

            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task endpoints_that_disable_csrf_should_not_require_csrf_header()
        {
            await BffHost.BffLoginAsync("alice");

            var req = new HttpRequestMessage(HttpMethod.Get, BffHost.Url("/api_user_no_csrf/test"));
            var response = await BffHost.BrowserClient.SendAsync(req);

            response.IsSuccessStatusCode.Should().BeTrue();
            response.Content.Headers.ContentType.MediaType.Should().Be("application/json");
            var json = await response.Content.ReadAsStringAsync();
            var apiResult = JsonSerializer.Deserialize<ApiResponse>(json);
            apiResult.Method.Should().Be("GET");
            apiResult.Path.Should().Be("/test");
            apiResult.Sub.Should().Be("alice");
            apiResult.ClientId.Should().Be("spa");
        }

        [Fact]
        public async Task calls_to_endpoint_without_bff_metadata_should_fail()
        {
            var req = new HttpRequestMessage(HttpMethod.Get, BffHost.Url("/not_bff_endpoint"));

            Func<Task> f = () => BffHost.BrowserClient.SendAsync(req);
            await f.Should().ThrowAsync<Exception>();
        }
        
        [Fact]
        public async Task calls_to_bff_not_in_endpoint_routing_should_fail()
        {
            var req = new HttpRequestMessage(HttpMethod.Get, BffHost.Url("/invalid_endpoint/test"));

            Func<Task> f = () => BffHost.BrowserClient.SendAsync(req);
            await f.Should().ThrowAsync<Exception>();
        }

        [Fact]
        public async Task test_dpop()
        {
            var rsaKey = new RsaSecurityKey(RSA.Create(2048));
            var jsonWebKey = JsonWebKeyConverter.ConvertFromRSASecurityKey(rsaKey);
            jsonWebKey.Alg = "PS256";
            var jwk = JsonSerializer.Serialize(jsonWebKey);

            BffHost.OnConfigureServices += svcs =>
            {
                svcs.PostConfigure<BffOptions>(opts =>
                {
                    opts.DPoPJsonWebKey = jwk;
                });
            };
            BffHost.InitializeAsync().Wait();

            var req = new HttpRequestMessage(HttpMethod.Get, BffHost.Url("/api_client/test"));
            req.Headers.Add("x-csrf", "1");
            var response = await BffHost.BrowserClient.SendAsync(req);

            response.IsSuccessStatusCode.Should().BeTrue();
            response.Content.Headers.ContentType.MediaType.Should().Be("application/json");
            var json = await response.Content.ReadAsStringAsync();
            var apiResult = JsonSerializer.Deserialize<ApiResponse>(json);
            apiResult.RequestHeaders["DPoP"].First().Should().NotBeNullOrEmpty();
            apiResult.RequestHeaders["Authorization"].First().StartsWith("DPoP ").Should().BeTrue();
        }

        [Fact]
        public async Task remote_endpoint_should_remove_set_cookie_and_clear_site_data_from_api_response()
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
        public async Task calls_to_remote_endpoint_that_renew_the_user_token_should_forward_the_bff_cookie_but_not_the_api_cookie()
        {
            await BffHost.BffLoginAsync("alice");
            var sessionCookieNames = BffHost.BrowserClient.CookieContainer.GetCookies(new Uri(BffHost.Url()))
                .Cast<Cookie>()
                .Select(cookie => cookie.Name)
                .ToArray();

            ApiHost.ResponseHeadersToReturn["Set-Cookie"] = "from-api=1; path=/";

            var req = new HttpRequestMessage(HttpMethod.Get, BffHost.Url("/api_user_force_renewal/test"));
            req.Headers.Add("x-csrf", "1");
            var response = await BffHost.BrowserClient.SendAsync(req);

            response.IsSuccessStatusCode.Should().BeTrue();

            var setCookieValues = response.Headers.TryGetValues("Set-Cookie", out var values)
                ? values.ToArray()
                : Array.Empty<string>();
            var setCookieNames = setCookieValues.Select(v => v.Split('=')[0]).ToArray();

            setCookieNames.Should().NotContain("from-api");
            setCookieNames.Should().Contain(name => sessionCookieNames.Contains(name),
                "the BFF re-issues its session cookie after it renews the user token");
        }

        [Fact]
        public async Task remote_endpoint_should_keep_set_cookie_and_clear_site_data_from_api_response_when_opted_out()
        {
            BffHost.OnConfigureServices += svcs =>
            {
                svcs.PostConfigure<BffOptions>(opts =>
                {
                    opts.RemoveCookieHeadersFromProxiedResponses = false;
                });
            };
            BffHost.InitializeAsync().Wait();

            await BffHost.BffLoginAsync("alice");

            ApiHost.ResponseHeadersToReturn["Set-Cookie"] = "from-api=1; path=/";
            ApiHost.ResponseHeadersToReturn["Clear-Site-Data"] = "\"cookies\"";

            var req = new HttpRequestMessage(HttpMethod.Get, BffHost.Url("/api_user/test"));
            req.Headers.Add("x-csrf", "1");
            var response = await BffHost.BrowserClient.SendAsync(req);

            response.IsSuccessStatusCode.Should().BeTrue();

            var setCookieValues = response.Headers.TryGetValues("Set-Cookie", out var values)
                ? values
                : Enumerable.Empty<string>();
            setCookieValues.Should().Contain(v => v.StartsWith("from-api=", StringComparison.Ordinal));
            response.Headers.Contains("Clear-Site-Data").Should().BeTrue();
        }
    }
}
