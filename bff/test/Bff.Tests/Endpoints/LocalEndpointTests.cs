// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using System.Text.Json;
using Duende.Bff.Tests.TestFramework;
using Duende.Bff.Tests.TestInfra;
using Microsoft.AspNetCore.Authentication;
using Xunit.Abstractions;

namespace Duende.Bff.Tests.Endpoints;

public class LocalEndpointTests(ITestOutputHelper output) : BffTestBase(output)
{
    public HttpStatusCode LocalApiResponseStatus { get; set; } = HttpStatusCode.OK;

    private async Task ReturnApiCallDetails(HttpContext context)
    {
        var sub = context.User.FindFirst("sub")?.Value;
        var body = default(string);
        if (context.Request.HasJsonContentType())
        {
            using (var sr = new StreamReader(context.Request.Body))
            {
                body = await sr.ReadToEndAsync();
            }
        }

        var response = new ApiCallDetails(
            HttpMethod.Parse(context.Request.Method),
            context.Request.Path.Value ?? "/",
            sub,
            context.User.FindFirst("client_id")?.Value,
            context.User.Claims.Select(x => new TestClaimRecord(x.Type, x.Value)).ToArray())
        {
            Body = body
        };

        if (LocalApiResponseStatus == HttpStatusCode.OK)
        {
            context.Response.StatusCode = 200;

            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(response));
        }
        else if (LocalApiResponseStatus == HttpStatusCode.Unauthorized)
        {
            await context.ChallengeAsync();
        }
        else if (LocalApiResponseStatus == HttpStatusCode.Forbidden)
        {
            await context.ForbidAsync();
        }
        else
        {
            throw new Exception("Invalid LocalApiResponseStatus");
        }
    }

    [Theory]
    [MemberData(nameof(AllSetups))]
    public async Task calls_to_authorized_local_endpoint_should_succeed(BffSetupType setup)
    {
        Bff.OnConfigureEndpoints += endpoints =>
        {
            endpoints.Map(The.Path, ReturnApiCallDetails)
                .RequireAuthorization()
                .AsBffApiEndpoint();
        };

        ConfigureBff(setup);
        await InitializeAsync();

        await Bff.BrowserClient.Login();

        ApiCallDetails apiResult = await Bff.BrowserClient.CallBffHostApi(
            url: Bff.Url(The.Path)
        );

        apiResult.Method.ShouldBe(HttpMethod.Get);
        apiResult.Path.ShouldBe(The.Path);
        apiResult.Sub.ShouldBe(The.Sub);
    }

    [Theory]
    [MemberData(nameof(AllSetups))]
    public async Task calls_to_authorized_local_endpoint_without_csrf_should_succeed_without_antiforgery_header(BffSetupType setup)
    {
        Bff.OnConfigureEndpoints += endpoints =>
        {
            endpoints.Map(The.Path, ReturnApiCallDetails)
                .RequireAuthorization()
                .SkipAntiforgery()
                .AsBffApiEndpoint();
        };
        ConfigureBff(setup);
        await InitializeAsync();

        await Bff.BrowserClient.Login();

        ApiCallDetails apiResult = await Bff.BrowserClient.CallBffHostApi(
            url: Bff.Url(The.Path),
            headers: []
        );

        apiResult.Method.ShouldBe(HttpMethod.Get);
        apiResult.Path.ShouldBe(The.Path);
        apiResult.Sub.ShouldBe(The.Sub);
    }

    [Theory]
    [MemberData(nameof(AllSetups))]
    public async Task unauthenticated_calls_to_authorized_local_endpoint_should_fail(BffSetupType setup)
    {
        Bff.OnConfigureEndpoints += endpoints =>
        {
            endpoints.Map(The.Path, ReturnApiCallDetails)
                .RequireAuthorization()
                .AsBffApiEndpoint();
        };
        ConfigureBff(setup);
        await InitializeAsync();



        ApiCallDetails apiResult = await Bff.BrowserClient.CallBffHostApi(
            url: Bff.Url(The.Path),
            expectedStatusCode: HttpStatusCode.Unauthorized
        );
    }

    [Theory]
    [MemberData(nameof(AllSetups))]
    public async Task calls_to_local_endpoint_should_require_antiforgery_header(BffSetupType setup)
    {

        Bff.OnConfigureEndpoints += endpoints =>
        {
            endpoints.Map(The.Path, ReturnApiCallDetails)
                .AsBffApiEndpoint();
        };

        ConfigureBff(setup);
        await InitializeAsync();


        ApiCallDetails apiResult = await Bff.BrowserClient.CallBffHostApi(
            url: Bff.Url(The.Path),
            headers: [],
            expectedStatusCode: HttpStatusCode.Unauthorized
        );
    }

    [Theory]
    [MemberData(nameof(AllSetups))]
    public async Task calls_to_local_endpoint_without_csrf_should_not_require_antiforgery_header(BffSetupType setup)
    {
        Bff.OnConfigureEndpoints += endpoints =>
        {
            endpoints.Map(The.Path, ReturnApiCallDetails)
                .SkipAntiforgery()
                .AsBffApiEndpoint();
        };
        ConfigureBff(setup);
        await InitializeAsync();



        ApiCallDetails apiResult = await Bff.BrowserClient.CallBffHostApi(
            url: Bff.Url(The.Path),
            headers: []
        );

        apiResult.Sub.ShouldBeNull();
    }

    [Theory]
    [MemberData(nameof(AllSetups))]
    public async Task calls_to_anon_endpoint_should_allow_anonymous(BffSetupType setup)
    {
        Bff.OnConfigureEndpoints += endpoints =>
        {
            endpoints.Map(The.Path, ReturnApiCallDetails)
                .AsBffApiEndpoint();
        };
        ConfigureBff(setup);
        await InitializeAsync();



        ApiCallDetails apiResult = await Bff.BrowserClient.CallBffHostApi(
            url: Bff.Url(The.Path)
        );

        apiResult.Sub.ShouldBeNull();
    }

    [Theory]
    [MemberData(nameof(AllSetups))]
    public async Task put_to_local_endpoint_should_succeed(BffSetupType setup)
    {
        Bff.OnConfigureEndpoints += endpoints =>
        {
            endpoints.MapPut(The.Path, ReturnApiCallDetails)
                .AsBffApiEndpoint();
        };
        ConfigureBff(setup);
        await InitializeAsync();



        await Bff.BrowserClient.Login();

        ApiCallDetails apiResult = await Bff.BrowserClient.CallBffHostApi(
            url: Bff.Url(The.Path),
            method: HttpMethod.Put,
            content: JsonContent.Create(new TestPayload("hello test api"))
        );

        apiResult.Method.ShouldBe(HttpMethod.Put);
        apiResult.Path.ShouldBe(The.Path);
        apiResult.Sub.ShouldBe(The.Sub);
        var body = apiResult.BodyAs<TestPayload>();
        body.Message.ShouldBe("hello test api", apiResult.Body);
    }

    [Theory]
    [MemberData(nameof(AllSetups))]
    public async Task unauthenticated_non_bff_endpoint_should_return_302_for_login(BffSetupType setup)
    {
        Bff.OnConfigureEndpoints += endpoints =>
        {
            endpoints.MapGet(The.Path, ReturnApiCallDetails)
                .RequireAuthorization();
        };
        ConfigureBff(setup);
        await InitializeAsync();


        Bff.BrowserClient.RedirectHandler.AutoFollowRedirects = false; // we want to see the redirect
        var response = await Bff.BrowserClient.CallBffHostApi(
            url: Bff.Url(The.Path),
            expectedStatusCode: HttpStatusCode.Redirect
        );

        response.HttpResponse.Headers.Location
            .ShouldNotBeNull()
            .ToString()
            .ToLowerInvariant()
            .ShouldStartWith(IdentityServer.Url("/connect/authorize").ToString());
    }

    [Theory]
    [MemberData(nameof(AllSetups))]
    public async Task unauthenticated_api_call_should_return_401(BffSetupType setup)
    {
        Bff.OnConfigureEndpoints += endpoints =>
        {
            endpoints.MapGet(The.Path, ReturnApiCallDetails)
                .RequireAuthorization()
                .AsBffApiEndpoint();
        };
        ConfigureBff(setup);
        await InitializeAsync();


        LocalApiResponseStatus = HttpStatusCode.Unauthorized;

        var response = await Bff.BrowserClient.CallBffHostApi(
            url: Bff.Url(The.Path),
            expectedStatusCode: HttpStatusCode.Unauthorized
        );
    }

    [Theory]
    [MemberData(nameof(AllSetups))]
    public async Task forbidden_api_call_should_return_403(BffSetupType setup)
    {
        Bff.OnConfigureEndpoints += endpoints =>
        {
            endpoints.MapGet(The.Path, ReturnApiCallDetails)
                .RequireAuthorization()
                .AsBffApiEndpoint();
        };

        ConfigureBff(setup);
        await InitializeAsync();

        LocalApiResponseStatus = HttpStatusCode.Forbidden;

        await Bff.BrowserClient.Login();
        Bff.BrowserClient.RedirectHandler.AutoFollowRedirects = false;
        var response = await Bff.BrowserClient.CallBffHostApi(
            url: Bff.Url(The.Path),
            expectedStatusCode: HttpStatusCode.Forbidden
        );
    }

    [Theory]
    [MemberData(nameof(AllSetups))]
    public async Task challenge_response_should_return_401(BffSetupType setup)
    {
        Bff.OnConfigureEndpoints += endpoints =>
        {
            endpoints.MapGet(The.Path, c => c.ChallengeAsync())
                .RequireAuthorization()
                .AsBffApiEndpoint();
        };

        ConfigureBff(setup);
        await InitializeAsync();


        await Bff.BrowserClient.Login();

        var response = await Bff.BrowserClient.CallBffHostApi(
            url: Bff.Url(The.Path),
            expectedStatusCode: HttpStatusCode.Unauthorized
        );
    }

    [Theory]
    [MemberData(nameof(AllSetups))]
    public async Task challenge_response_when_response_handling_skipped_should_trigger_redirect_for_login(BffSetupType setup)
    {
        Bff.OnConfigureEndpoints += endpoints =>
        {
            endpoints.MapGet(The.Path, c => c.ChallengeAsync())
                .RequireAuthorization()
                .AsBffApiEndpoint()
                .SkipResponseHandling();
        };

        ConfigureBff(setup);
        await InitializeAsync();

        await Bff.BrowserClient.Login();
        Bff.BrowserClient.RedirectHandler.AutoFollowRedirects = false;
        var response = await Bff.BrowserClient.CallBffHostApi(
            url: Bff.Url(The.Path),
            expectedStatusCode: HttpStatusCode.Redirect
        );
    }

    [Theory]
    [MemberData(nameof(AllSetups))]
    public async Task fallback_policy_should_not_fail(BffSetupType setup)
    {

        Bff.OnConfigureServices += svcs =>
        {
            svcs.AddAuthorization(opts =>
            {
                opts.FallbackPolicy =
                    new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
                        .RequireAuthenticatedUser()
                        .Build();
            });
        };
        ConfigureBff(setup);
        await InitializeAsync();


        var response = await Bff.BrowserClient.GetAsync(Bff.Url("/not-found"));
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
