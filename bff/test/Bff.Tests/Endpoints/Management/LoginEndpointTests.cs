// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using Duende.Bff.Configuration;
using Duende.Bff.Tests.TestInfra;
using Xunit.Abstractions;

namespace Duende.Bff.Tests.Endpoints.Management;

public class LoginEndpointTests : BffTestBase
{
    public LoginEndpointTests(ITestOutputHelper output) : base(output) => Bff.SetBffOptions += options =>
                                                                               {
                                                                                   options.ConfigureOpenIdConnectDefaults = opt =>
                                                                                   {
                                                                                       The.DefaultOpenIdConnectConfiguration(opt);
                                                                                   };
                                                                               };

    [Theory]
    [MemberData(nameof(AllSetups))]
    public async Task login_should_allow_anonymous(BffSetupType setup)
    {
        ConfigureBff(setup);

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
        await InitializeAsync();

        var response = await Bff.BrowserClient.Login();
        response.StatusCode.ShouldNotBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [MemberData(nameof(AllSetups))]
    public async Task when_unauthenticated_silent_login_should_return_isLoggedIn_false(BffSetupType setup)
    {
        ConfigureBff(setup);
        await InitializeAsync();

        var response = await Bff.BrowserClient.GetAsync(Bff.Url("/bff/silent-login?redirectUri=/"))
            .CheckHttpStatusCode();
        response.Content.Headers.ContentType!.MediaType.ShouldBe("text/html");
        response.RequestMessage!.RequestUri.ShouldBe(Bff.Url("/bff/silent-login-callback"));
        var message = await response.Content.ReadAsStringAsync();
        message.ShouldContain("source:'bff-silent-login");
        message.ShouldContain("isLoggedIn:false");
    }

    [Theory]
    [MemberData(nameof(AllSetups))]
    public async Task silent_login_should_challenge_and_return_silent_login_html(BffSetupType setup)
    {
        ConfigureBff(setup);
        await InitializeAsync();

        await Bff.BrowserClient.Login();

        var response = await Bff.BrowserClient.GetAsync(Bff.Url("/bff/silent-login?redirectUri=/"))
            .CheckHttpStatusCode();

        response.Content.Headers.ContentType!.MediaType.ShouldBe("text/html");

        response.RequestMessage!.RequestUri.ShouldBe(Bff.Url("/bff/silent-login-callback"));

        var message = await response.Content.ReadAsStringAsync();
        message.ShouldContain("source:'bff-silent-login");
        message.ShouldContain($"isLoggedIn:true");
    }

    [Theory]
    [MemberData(nameof(AllSetups))]
    public async Task can_issue_silent_login_with_prompt_none(BffSetupType setup)
    {
        ConfigureBff(setup);
        await InitializeAsync();

        await Bff.BrowserClient.Login();

        var response = await Bff.BrowserClient.GetAsync(Bff.Url("/bff/login?prompt=none"))
            .CheckHttpStatusCode();

        response.Content.Headers.ContentType!.MediaType.ShouldBe("text/html");

        response.RequestMessage!.RequestUri.ShouldBe(Bff.Url("/bff/silent-login-callback"));

        var message = await response.Content.ReadAsStringAsync();
        message.ShouldContain("source:'bff-silent-login");
        message.ShouldContain($"isLoggedIn:true");
    }

    [Theory]
    [MemberData(nameof(AllSetups))]
    public async Task login_with_unsupported_prompt_is_rejected(BffSetupType setup)
    {
        ConfigureBff(setup);
        await InitializeAsync();

        var response = await Bff.BrowserClient.GetAsync(Bff.Url("/bff/login?prompt=not_supported_prompt"));
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();
        problem!.Errors.ShouldContainKey("prompt");
        problem!.Errors["prompt"].ShouldContain("prompt 'not_supported_prompt' is not supported");
    }

    [Theory]
    [MemberData(nameof(AllSetups))]
    public async Task can_use_prompt_supported_by_IdentityServer(BffSetupType setup)
    {
        ConfigureBff(setup);
        await InitializeAsync();

        // Prompt=create is enabled in identity server configuration:
        // https://docs.duendesoftware.com/identityserver/reference/options#userinteraction
        // by setting CreateAccountUrl 

        var response = await Bff.BrowserClient.GetAsync(Bff.Url("/bff/login?prompt=create"))
            .CheckHttpStatusCode();

        response.RequestMessage!.RequestUri!.ToString().ShouldStartWith(IdentityServer.Url("/account/create").ToString());
        response.RequestMessage!.RequestUri!.ToString().ShouldNotContain("error");
    }

    [Theory]
    [MemberData(nameof(AllSetups))]
    public async Task login_endpoint_should_authenticatre_and_redirect_to_root(BffSetupType setup)
    {
        ConfigureBff(setup);
        await InitializeAsync();

        var response = await Bff.BrowserClient.Login();
        response.RequestMessage!.RequestUri.ShouldBe(Bff.Url("/"));

    }

    [Theory]
    [MemberData(nameof(AllSetups))]
    public async Task login_endpoint_should_challenge_and_redirect_to_root_with_custom_prefix(BffSetupType setup)
    {
        ConfigureBff(setup);
        Bff.OnConfigureServices += svcs =>
        {
            svcs.Configure<BffOptions>(options =>
            {
                options.ManagementBasePath = "/custom/bff";
            });
        };
        await InitializeAsync();

        await Bff.BrowserClient.Login(expectedStatusCode: HttpStatusCode.NotFound);

        var response = await Bff.BrowserClient.Login("/custom");
        response.RequestMessage!.RequestUri.ShouldBe(Bff.Url("/"));

    }

    [Theory]
    [MemberData(nameof(AllSetups))]
    public async Task login_endpoint_should_challenge_and_redirect_to_root_with_custom_prefix_trailing_slash(BffSetupType setup)
    {
        ConfigureBff(setup);
        Bff.OnConfigureServices += svcs =>
        {
            svcs.Configure<BffOptions>(options =>
            {
                options.ManagementBasePath = "/custom/bff/";
            });
        };
        await InitializeAsync();

        await Bff.BrowserClient.Login(expectedStatusCode: HttpStatusCode.NotFound);

        var response = await Bff.BrowserClient.Login("/custom");
        response.RequestMessage!.RequestUri.ShouldBe(Bff.Url("/"));
    }

    [Theory]
    [MemberData(nameof(AllSetups))]
    public async Task login_endpoint_should_challenge_and_redirect_to_root_with_root_prefix(BffSetupType setup)
    {
        ConfigureBff(setup);
        Bff.OnConfigureServices += svcs =>
        {
            svcs.Configure<BffOptions>(options =>
            {
                options.ManagementBasePath = "/";
            });
        };
        await InitializeAsync();

        var response = await Bff.BrowserClient.GetAsync(Bff.Url("/login"))
            .CheckHttpStatusCode();

        response.RequestMessage!.RequestUri.ShouldBe(Bff.Url("/"));

    }

    [Theory]
    [MemberData(nameof(AllSetups))]
    public async Task login_endpoint_with_existing_session_should_challenge(BffSetupType setup)
    {
        ConfigureBff(setup);
        await InitializeAsync();

        await Bff.BrowserClient.Login();

        // Disable auto redirects, to see if we get a challenge
        Bff.BrowserClient.RedirectHandler.AutoFollowRedirects = false;

        var response = await Bff.BrowserClient.Login(expectedStatusCode: HttpStatusCode.Redirect);
        response.Headers.Location!.ToString().ShouldStartWith(IdentityServer.Url("/connect/authorize").ToString());
    }

    [Theory]
    [MemberData(nameof(AllSetups))]
    public async Task login_endpoint_should_accept_returnUrl(BffSetupType setup)
    {
        ConfigureBff(setup);
        Bff.OnConfigureEndpoints += endpoints => endpoints.MapGet("/foo", () => "foo'd you");

        await InitializeAsync();

        var response = await Bff.BrowserClient.GetAsync(Bff.Url("/bff/login") + "?returnUrl=/foo")
            .CheckHttpStatusCode();

        var result = await response.Content.ReadAsStringAsync();
        result.ShouldBe("foo'd you");
    }

    [Theory]
    [MemberData(nameof(AllSetups))]
    public async Task login_endpoint_should_not_accept_non_local_returnUrl(BffSetupType setup)
    {
        ConfigureBff(setup);
        await InitializeAsync();

        var problem = await Bff.BrowserClient.GetAsync(Bff.Url("/bff/login") + "?returnUrl=https://foo")
            .ShouldBeProblem();

        problem.Errors.ShouldContainKey(Constants.RequestParameters.ReturnUrl);
    }
}
