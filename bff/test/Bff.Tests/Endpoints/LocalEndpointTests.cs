// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using Duende.Bff.DynamicFrontends;
using Duende.Bff.Tests.TestFramework;
using Duende.Bff.Tests.TestInfra;
using Microsoft.AspNetCore.Authentication;
using Xunit.Abstractions;

namespace Duende.Bff.Tests.Endpoints;

public class LocalEndpointTests(ITestOutputHelper output) : BffTestBase(output)
{
    public HttpStatusCode LocalApiResponseStatus { get; set; } = HttpStatusCode.OK;

    [Theory]
    [MemberData(nameof(AllSetups))]
    public async Task calls_to_authorized_local_endpoint_should_succeed(BffSetupType setup)
    {
        Bff.OnConfigureApp += app =>
        {
            app.Map(The.Path, c => ApiHost.ReturnApiCallDetails(c, () => LocalApiResponseStatus))
                .RequireAuthorization()
                .AsBffApiEndpoint();
        };

        await ConfigureBff(setup);

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
        Bff.OnConfigureApp += app =>
        {
            app.Map(The.Path, c => ApiHost.ReturnApiCallDetails(c, () => LocalApiResponseStatus))
                .RequireAuthorization()
                .SkipAntiforgery()
                .AsBffApiEndpoint();
        };
        await ConfigureBff(setup);

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
        Bff.OnConfigureApp += app =>
        {
            app.Map(The.Path, c => ApiHost.ReturnApiCallDetails(c, () => LocalApiResponseStatus))
                .RequireAuthorization()
                .AsBffApiEndpoint();
        };
        await ConfigureBff(setup);



        ApiCallDetails apiResult = await Bff.BrowserClient.CallBffHostApi(
            url: Bff.Url(The.Path),
            expectedStatusCode: HttpStatusCode.Unauthorized
        );
    }

    [Theory]
    [MemberData(nameof(AllSetups))]
    public async Task calls_to_local_endpoint_should_require_antiforgery_header(BffSetupType setup)
    {

        Bff.OnConfigureApp += app =>
        {
            app.Map(The.Path, c => ApiHost.ReturnApiCallDetails(c, () => LocalApiResponseStatus))
                .AsBffApiEndpoint();
        };

        await ConfigureBff(setup);


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
        Bff.OnConfigureApp += app =>
        {
            app.Map(The.Path, c => ApiHost.ReturnApiCallDetails(c, () => LocalApiResponseStatus))
                .SkipAntiforgery()
                .AsBffApiEndpoint();
        };
        await ConfigureBff(setup);



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
        Bff.OnConfigureApp += app =>
        {
            app.Map(The.Path, c => ApiHost.ReturnApiCallDetails(c, () => LocalApiResponseStatus))
                .AsBffApiEndpoint();
        };
        await ConfigureBff(setup);



        ApiCallDetails apiResult = await Bff.BrowserClient.CallBffHostApi(
            url: Bff.Url(The.Path)
        );

        apiResult.Sub.ShouldBeNull();
    }

    [Theory]
    [MemberData(nameof(AllSetups))]
    public async Task put_to_local_endpoint_should_succeed(BffSetupType setup)
    {
        Bff.OnConfigureApp += app =>
        {
            app.Map(The.Path, c => ApiHost.ReturnApiCallDetails(c, () => LocalApiResponseStatus))
                .AsBffApiEndpoint();
        };
        await ConfigureBff(setup);



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
        Bff.OnConfigureApp += app =>
        {
            app.Map(The.Path, c => ApiHost.ReturnApiCallDetails(c, () => LocalApiResponseStatus))
                .RequireAuthorization();
        };
        await ConfigureBff(setup);



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
        Bff.OnConfigureApp += app =>
        {
            app.Map(The.Path, c => ApiHost.ReturnApiCallDetails(c, () => LocalApiResponseStatus))
                .RequireAuthorization()
                .AsBffApiEndpoint();
        };
        await ConfigureBff(setup);



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
        Bff.OnConfigureApp += app =>
        {
            app.Map(The.Path, c => ApiHost.ReturnApiCallDetails(c, () => LocalApiResponseStatus))
                .RequireAuthorization()
                .AsBffApiEndpoint();
        };

        await ConfigureBff(setup);
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
        Bff.OnConfigureApp += app =>
        {
            app.MapGet(The.Path, c => c.ChallengeAsync())
                .RequireAuthorization()
                .AsBffApiEndpoint();
        };

        await ConfigureBff(setup);



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
        Bff.OnConfigureApp += app =>
        {
            app.MapGet(The.Path, c => c.ChallengeAsync())
                .RequireAuthorization()
                .AsBffApiEndpoint()
                .SkipResponseHandling();
        };

        await ConfigureBff(setup);



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
        await ConfigureBff(setup);


        var response = await Bff.BrowserClient.GetAsync(Bff.Url("/not-found"));
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]

    public async Task authorization_policy_failure_should_return_403()
    {
        var identityServer = new IdentityServerTestHost(Context);
        var bff = new BffTestHost(Context, identityServer);
        identityServer.AddClient(The.ClientId, bff.Url());

        bff.OnConfigureBffOptions += opt =>
        {
            opt.BackchannelHttpHandler = Internet;
            opt.ConfigureOpenIdConnectDefaults = The.DefaultOpenIdConnectConfiguration;
        };

        // this is regards to an issue reported by one of our users:
        // https://github.com/orgs/DuendeSoftware/discussions/488
        // We have to explicitly configure the authentication schemes, but
        // not set the DefaultForbidScheme, otherwise when an authorization policy
        // fails, the BFF doesn't know which scheme to use for the forbid response,
        // and that causes a StackOverflowException.
        bff.OnConfigureServices += s => s.AddAuthentication(options =>
        {
            options.DefaultScheme = BffAuthenticationSchemes.BffCookie;
            options.DefaultChallengeScheme = BffAuthenticationSchemes.BffOpenIdConnect;
            options.DefaultSignOutScheme = BffAuthenticationSchemes.BffOpenIdConnect;
        });

        // This test verifies that when an authorization policy fails (not just RequireAuthenticatedUser,
        // but a custom policy like RequireClaim), the BFF correctly returns 403 without causing
        // a StackOverflowException. This was a bug when DefaultForbidScheme was not set.
        AddCustomUserClaims(new System.Security.Claims.Claim("given_name", "Alice"));

        bff.OnConfigureApp += app =>
        {
            app.Map(The.Path, c => ApiHost.ReturnApiCallDetails(c, () => LocalApiResponseStatus))
                .RequireAuthorization(policy =>
                {
                    policy.RequireAuthenticatedUser();
                    policy.RequireClaim("given_name", "Bob"); // Alice won't have this claim
                })
                .AsBffApiEndpoint();
        };

        await identityServer.InitializeAsync();
        await bff.InitializeAsync();

        await bff.BrowserClient.Login();
        bff.BrowserClient.RedirectHandler.AutoFollowRedirects = false;

        await bff.BrowserClient.CallBffHostApi(
            url: Bff.Url(The.Path),
            expectedStatusCode: HttpStatusCode.Forbidden
        );
    }
}
