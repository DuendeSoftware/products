// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using Duende.Bff.Tests.TestFramework;
using Duende.Bff.Tests.TestHosts;
using Xunit.Abstractions;

namespace Duende.Bff.Tests.Endpoints;

public class LocalEndpointTests(ITestOutputHelper output) : BffIntegrationTestBase(output)
{
    [Fact]
    public async Task calls_to_authorized_local_endpoint_should_succeed()
    {
        await BffHost.BffLoginAsync("alice");

        ApiResponse apiResult = await BffHost.BrowserClient.CallBffHostApi(
            url: BffHost.Url("/local_authz")
        );

        apiResult.Method.ShouldBe("GET");
        apiResult.Path.ShouldBe("/local_authz");
        apiResult.Sub.ShouldBe("alice");
    }

    [Fact]
    public async Task calls_to_authorized_local_endpoint_without_csrf_should_succeed_without_antiforgery_header()
    {
        await BffHost.BffLoginAsync("alice");

        ApiResponse apiResult = await BffHost.BrowserClient.CallBffHostApi(
            url: BffHost.Url("/local_authz_no_csrf")
        );

        apiResult.Method.ShouldBe("GET");
        apiResult.Path.ShouldBe("/local_authz_no_csrf");
        apiResult.Sub.ShouldBe("alice");
    }

    [Fact]
    public async Task unauthenticated_calls_to_authorized_local_endpoint_should_fail()
    {
        var response = await BffHost.BrowserClient.CallBffHostApi(
            url: BffHost.Url("/local_authz"),
            expectedStatusCode: HttpStatusCode.Unauthorized
        );
    }

    [Fact]
    public async Task calls_to_local_endpoint_should_require_antiforgery_header()
    {
        var req = new HttpRequestMessage(HttpMethod.Get, BffHost.Url("/local_anon"));
        var response = await BffHost.BrowserClient.SendAsync(req);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }


    [Fact]
    public async Task calls_to_local_endpoint_without_csrf_should_not_require_antiforgery_header()
    {
        var response = await BffHost.BrowserClient.CallBffHostApi(
            url: BffHost.Url("/local_anon_no_csrf"),
            expectedStatusCode: HttpStatusCode.OK
        );
    }

    [Fact]
    public async Task calls_to_anon_endpoint_should_allow_anonymous()
    {
        ApiResponse apiResult = await BffHost.BrowserClient.CallBffHostApi(
            url: BffHost.Url("/local_anon")
        );

        apiResult.Method.ShouldBe("GET");
        apiResult.Path.ShouldBe("/local_anon");
        apiResult.Sub.ShouldBeNull();
    }

    [Fact]
    public async Task put_to_local_endpoint_should_succeed()
    {
        await BffHost.BffLoginAsync("alice");

        ApiResponse apiResult = await BffHost.BrowserClient.CallBffHostApi(
            url: BffHost.Url("/local_authz"),
            method: HttpMethod.Put,
            content: JsonContent.Create(new TestPayload("hello test api"))
        );

        apiResult.Method.ShouldBe("PUT");
        apiResult.Path.ShouldBe("/local_authz");
        apiResult.Sub.ShouldBe("alice");
        var body = apiResult.BodyAs<TestPayload>();
        body.Message.ShouldBe("hello test api", apiResult.Body);
    }

    [Fact]
    public async Task unauthenticated_non_bff_endpoint_should_return_302_for_login()
    {
        var req = new HttpRequestMessage(HttpMethod.Get, BffHost.Url("/always_fail_authz_non_bff_endpoint"));
        req.Headers.Add("x-csrf", "1");
        var response = await BffHost.BrowserClient.SendAsync(req);

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location
            .ShouldNotBeNull()
            .ToString()
            .ToLowerInvariant()
            .ShouldStartWith(IdentityServerHost.Url("/connect/authorize"));
    }

    [Fact]
    public async Task unauthenticated_api_call_should_return_401()
    {
        var response = await BffHost.BrowserClient.CallBffHostApi(
            url: BffHost.Url("/always_fail_authz"),
            expectedStatusCode: HttpStatusCode.Unauthorized
        );
    }

    [Fact]
    public async Task forbidden_api_call_should_return_403()
    {
        await BffHost.BffLoginAsync("alice");

        var response = await BffHost.BrowserClient.CallBffHostApi(
            url: BffHost.Url("/always_fail_authz"),
            expectedStatusCode: HttpStatusCode.Forbidden
        );
    }

    [Fact]
    public async Task challenge_response_should_return_401()
    {
        await BffHost.BffLoginAsync("alice");
        BffHost.LocalApiResponseStatus = BffHost.ResponseStatus.Challenge;

        var response = await BffHost.BrowserClient.CallBffHostApi(
            url: BffHost.Url("/local_authz"),
            expectedStatusCode: HttpStatusCode.Unauthorized
        );
    }

    [Fact]
    public async Task forbid_response_should_return_403()
    {
        await BffHost.BffLoginAsync("alice");
        BffHost.LocalApiResponseStatus = BffHost.ResponseStatus.Forbid;

        var response = await BffHost.BrowserClient.CallBffHostApi(
            url: BffHost.Url("/local_authz"),
            expectedStatusCode: HttpStatusCode.Forbidden
        );
    }

    [Fact]
    public async Task challenge_response_when_response_handling_skipped_should_trigger_redirect_for_login()
    {
        await BffHost.BffLoginAsync("alice");
        BffHost.LocalApiResponseStatus = BffHost.ResponseStatus.Challenge;

        var response = await BffHost.BrowserClient.CallBffHostApi(
            url: BffHost.Url("/local_anon_no_csrf_no_response_handling"),
            expectedStatusCode: HttpStatusCode.Redirect
        );
    }

    [Fact]
    public async Task fallback_policy_should_not_fail()
    {
        BffHost.OnConfigureServices += svcs =>
        {
            svcs.AddAuthorization(opts =>
            {
                opts.FallbackPolicy =
                    new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
                        .RequireAuthenticatedUser()
                        .Build();
            });
        };
        await BffHost.InitializeAsync();

        var response = await BffHost.HttpClient.GetAsync(BffHost.Url("/not-found"));
        response.StatusCode.ShouldNotBe(HttpStatusCode.InternalServerError);
    }
}
