// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using System.Text.Json;
using Duende.Bff.Tests.TestFramework;
using Duende.Bff.Tests.TestHosts;
using Xunit.Abstractions;

namespace Duende.Bff.Tests.Endpoints;

public class RemoteEndpointTests(ITestOutputHelper output) : BffIntegrationTestBase(output)
{
    [Fact]
    public async Task unauthenticated_calls_to_remote_endpoint_should_return_401() => await Bff.BrowserClient.CallBffHostApi(
            url: Bff.Url("/api_user/test"),
            expectedStatusCode: HttpStatusCode.Unauthorized
        );

    [Fact]
    public async Task calls_to_remote_endpoint_should_forward_user_to_api()
    {
        await Bff.BffLoginAsync("alice");

        var (response, apiResult) = await Bff.BrowserClient.CallBffHostApi(
            url: Bff.Url("/api_user/test")
        );

        apiResult.Method.ShouldBe(HttpMethod.Get);
        apiResult.Path.ShouldBe("/test");
        apiResult.Sub.ShouldBe("alice");
        apiResult.ClientId.ShouldBe("spa");

        response.Headers.GetValues("added-by-custom-default-transform").ShouldBe(["some-value"],
            "this value is added by the CustomDefaultBffTransformBuilder()");
    }

    [Fact]
    public async Task
        calls_to_remote_endpoint_with_useraccesstokenparameters_having_stored_named_token_should_forward_user_to_api()
    {
        await BffHostWithNamedTokens.BffLoginAsync("alice");

        ApiCallDetails apiResult = await BffHostWithNamedTokens.BrowserClient.CallBffHostApi(
            url: BffHostWithNamedTokens.Url(
                "/api_user_with_useraccesstokenparameters_having_stored_named_token/test")
        );

        apiResult.Method.ShouldBe(HttpMethod.Get);
        apiResult.Path.ShouldBe("/test");
        apiResult.Sub.ShouldBe("alice");
        apiResult.ClientId.ShouldBe("spa");
    }

    [Fact]
    public async Task
        calls_to_remote_endpoint_with_useraccesstokenparameters_having_not_stored_corresponding_named_token_finds_no_matching_token_should_fail()
    {
        await BffHostWithNamedTokens.BffLoginAsync("alice");

        await BffHostWithNamedTokens.BrowserClient.CallBffHostApi(
            url: BffHostWithNamedTokens.Url(
                "/api_user_with_useraccesstokenparameters_having_not_stored_named_token/test"),
            expectedStatusCode: HttpStatusCode.Unauthorized
        );
    }

    [Fact]
    public async Task put_to_remote_endpoint_should_forward_user_to_api()
    {
        await Bff.BffLoginAsync("alice");

        ApiCallDetails apiResult = await Bff.BrowserClient.CallBffHostApi(
            url: Bff.Url("/api_user/test"),
            method: HttpMethod.Put,
            content: JsonContent.Create(new TestPayload("hello test api"))
        );

        apiResult.Method.ShouldBe(HttpMethod.Put);
        apiResult.Path.ShouldBe("/test");
        apiResult.Sub.ShouldBe("alice");
        apiResult.ClientId.ShouldBe("spa");
        var body = apiResult.BodyAs<TestPayload>();
        body.Message.ShouldBe("hello test api", apiResult.Body);
    }

    [Fact]
    public async Task post_to_remote_endpoint_should_forward_user_to_api()
    {
        await Bff.BffLoginAsync("alice");

        ApiCallDetails apiResult = await Bff.BrowserClient.CallBffHostApi(
            url: Bff.Url("/api_user/test"),
            method: HttpMethod.Post,
            content: JsonContent.Create(new TestPayload("hello test api"))
        );

        apiResult.Method.ShouldBe(HttpMethod.Post);
        apiResult.Path.ShouldBe("/test");
        apiResult.Sub.ShouldBe("alice");
        apiResult.ClientId.ShouldBe("spa");
        var body = apiResult.BodyAs<TestPayload>();
        body.Message.ShouldBe("hello test api", apiResult.Body);
    }

    [Fact]
    public async Task calls_to_remote_endpoint_should_forward_user_or_anonymous_to_api()
    {
        {
            ApiCallDetails apiResult = await Bff.BrowserClient.CallBffHostApi(
                url: Bff.Url("/api_user_or_anon/test")
            );

            apiResult.Method.ShouldBe(HttpMethod.Get);
            apiResult.Path.ShouldBe("/test");
            apiResult.Sub.ShouldBeNull();
            apiResult.ClientId.ShouldBeNull();
        }

        {
            await Bff.BffLoginAsync("alice");

            ApiCallDetails apiResult = await Bff.BrowserClient.CallBffHostApi(
                url: Bff.Url("/api_user_or_anon/test")
            );

            apiResult.Method.ShouldBe(HttpMethod.Get);
            apiResult.Path.ShouldBe("/test");
            apiResult.Sub.ShouldBe("alice");
            apiResult.ClientId.ShouldBe("spa");
        }
    }

    [Fact]
    public async Task calls_to_remote_endpoint_should_forward_client_token_to_api()
    {
        await Bff.BffLoginAsync("alice");

        ApiCallDetails apiResult = await Bff.BrowserClient.CallBffHostApi(
            url: Bff.Url("/api_client/test")
        );

        apiResult.Method.ShouldBe(HttpMethod.Get);
        apiResult.Path.ShouldBe("/test");
        apiResult.Sub.ShouldBeNull();
        apiResult.ClientId.ShouldBe("spa");
    }

    [Fact]
    public async Task calls_to_remote_endpoint_should_fail_when_token_retrieval_fails()
    {
        await Bff.BffLoginAsync("alice");

        await Bff.BrowserClient.CallBffHostApi(
            url: Bff.Url("/api_with_access_token_retrieval_that_fails"),
            expectedStatusCode: HttpStatusCode.Unauthorized
        );

        // user should be signed out
        var result = await Bff.GetIsUserLoggedInAsync();
        result.ShouldBeFalse();
    }

    [Fact]
    public async Task calls_to_remote_api_that_returns_forbidden_will_return_forbidden()
    {
        await Bff.BffLoginAsync("alice");

        await Bff.BrowserClient.CallBffHostApi(
            url: Bff.Url("/api_forbidden"),
            expectedStatusCode: HttpStatusCode.Forbidden
        );
    }

    [Fact]
    public async Task calls_to_remote_api_that_returns_unauthorized_will_return_unauthorized()
    {
        await Bff.BffLoginAsync("alice");

        await Bff.BrowserClient.CallBffHostApi(
            url: Bff.Url("/api_unauthenticated"),
            expectedStatusCode: HttpStatusCode.Unauthorized
        );
    }

    [Fact]
    public async Task calls_to_remote_endpoint_should_send_token_from_token_retriever_when_token_retrieval_succeeds()
    {
        await Bff.BffLoginAsync("alice");

        ApiCallDetails apiResult = await Bff.BrowserClient.CallBffHostApi(
            url: Bff.Url("/api_with_access_token_retriever")
        );

        apiResult.Sub.ShouldBe("123");
        apiResult.ClientId.ShouldBe("fake-client");
    }

    [Fact]
    public async Task calls_to_remote_endpoint_should_forward_user_or_client_to_api()
    {
        {
            ApiCallDetails apiResult = await Bff.BrowserClient.CallBffHostApi(
                url: Bff.Url("/api_user_or_client/test")
            );

            apiResult.Method.ShouldBe(HttpMethod.Get);
            apiResult.Path.ShouldBe("/test");
            apiResult.Sub.ShouldBeNull();
            apiResult.ClientId.ShouldBe("spa");
        }

        {
            await Bff.BffLoginAsync("alice");

            ApiCallDetails apiResult = await Bff.BrowserClient.CallBffHostApi(
                url: Bff.Url("/api_user_or_client/test")
            );

            apiResult.Method.ShouldBe(HttpMethod.Get);
            apiResult.Path.ShouldBe("/test");
            apiResult.Sub.ShouldBe("alice");
            apiResult.ClientId.ShouldBe("spa");
        }
    }

    [Fact]
    public async Task calls_to_remote_endpoint_with_anon_should_be_anon()
    {
        {
            ApiCallDetails apiResult = await Bff.BrowserClient.CallBffHostApi(
                url: Bff.Url("/api_anon_only/test")
            );

            apiResult.Method.ShouldBe(HttpMethod.Get);
            apiResult.Path.ShouldBe("/test");
            apiResult.Sub.ShouldBeNull();
            apiResult.ClientId.ShouldBeNull();
        }

        {
            await Bff.BffLoginAsync("alice");

            ApiCallDetails apiResult = await Bff.BrowserClient.CallBffHostApi(
                url: Bff.Url("/api_anon_only/test")
            );

            apiResult.Method.ShouldBe(HttpMethod.Get);
            apiResult.Path.ShouldBe("/test");
            apiResult.Sub.ShouldBeNull();
            apiResult.ClientId.ShouldBeNull();
        }
    }

    [Fact]
    public async Task calls_to_remote_endpoint_expecting_token_but_without_token_should_fail()
    {
        var client = IdentityServer.Clients.Single(x => x.ClientId == "spa");
        client.Enabled = false;

        await Bff.BrowserClient.CallBffHostApi(
            url: Bff.Url("/api_user_or_client/test"),
            expectedStatusCode: HttpStatusCode.Unauthorized
        );

        await Bff.BrowserClient.CallBffHostApi(
            url: Bff.Url("/api_client/test"),
            expectedStatusCode: HttpStatusCode.Unauthorized
        );
    }

    [Fact]
    public async Task response_status_401_from_remote_endpoint_should_return_401_from_bff()
    {
        await Bff.BffLoginAsync("alice");
        Api.ApiStatusCodeToReturn = 401;

        await Bff.BrowserClient.CallBffHostApi(
            url: Bff.Url("/api_user/test"),
            expectedStatusCode: HttpStatusCode.Unauthorized
        );
    }

    [Fact]
    public async Task response_status_403_from_remote_endpoint_should_return_403_from_bff()
    {
        await Bff.BffLoginAsync("alice");
        Api.ApiStatusCodeToReturn = 403;

        await Bff.BrowserClient.CallBffHostApi(
            url: Bff.Url("/api_user/test"),
            expectedStatusCode: HttpStatusCode.Forbidden
        );
    }
    [Fact]
    public async Task calls_to_remote_endpoint_should_require_csrf()
    {
        var req = new HttpRequestMessage(HttpMethod.Get, Bff.Url("/api_user_or_client/test"));
        var response = await Bff.BrowserClient.SendAsync(req);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task endpoints_that_disable_csrf_should_not_require_csrf_header()
    {
        await Bff.BffLoginAsync("alice");

        ApiCallDetails apiResult = await Bff.BrowserClient.CallBffHostApi(
            url: Bff.Url("/api_user_no_csrf/test")
        );

        apiResult.Method.ShouldBe(HttpMethod.Get);
        apiResult.Path.ShouldBe("/test");
        apiResult.Sub.ShouldBe("alice");
        apiResult.ClientId.ShouldBe("spa");
    }

    [Fact]
    public async Task endpoint_can_be_configured_with_custom_transform()
    {
        await Bff.BffLoginAsync("alice");

        var req = new HttpRequestMessage(HttpMethod.Get, Bff.Url("/api_custom_transform/test"));
        req.Headers.Add("x-csrf", "1");
        req.Headers.Add("my-header-to-be-copied-by-yarp", "copied-value");
        var response = await Bff.BrowserClient.SendAsync(req);

        response.IsSuccessStatusCode.ShouldBeTrue();
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/json");
        var json = await response.Content.ReadAsStringAsync();
        var apiResult = JsonSerializer.Deserialize<ApiCallDetails>(json).ShouldNotBeNull();
        apiResult.RequestHeaders["my-header-to-be-copied-by-yarp"].First().ShouldBe("copied-value");

        response.Content.Headers.Select(x => x.Key).ShouldNotContain("added-by-custom-default-transform",
            "a custom transform doesn't run the defaults");
    }
    [Fact]
    public async Task can_disable_anti_forgery_check()
    {
        Bff.BffOptions.DisableAntiForgeryCheck = (c) => true;

        var req = new HttpRequestMessage(HttpMethod.Get, Bff.Url("/api_user_or_anon/test"));
        var response = await Bff.BrowserClient.SendAsync(req);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
