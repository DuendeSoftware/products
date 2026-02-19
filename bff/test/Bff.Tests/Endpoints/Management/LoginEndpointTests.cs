// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using Duende.Bff.Configuration;
using Duende.Bff.Tests.TestInfra;
namespace Duende.Bff.Tests.Endpoints.Management;

public class LoginEndpointTests : BffTestBase
{
    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();
        Bff.BffOptions.ConfigureOpenIdConnectDefaults = opt => { The.DefaultOpenIdConnectConfiguration(opt); };
    }

    [Theory]
    [MemberData(nameof(AllSetups))]
    public async Task login_should_allow_anonymous(BffSetupType setup)
    {
        await ConfigureBff(setup);

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

        var response = await Bff.BrowserClient.Login();
        response.StatusCode.ShouldNotBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [MemberData(nameof(AllSetups))]
    public async Task when_unauthenticated_silent_login_should_return_isLoggedIn_false(BffSetupType setup)
    {
        await ConfigureBff(setup);

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
        await ConfigureBff(setup);

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
        await ConfigureBff(setup);

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
        await ConfigureBff(setup);

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
        await ConfigureBff(setup);

        // Prompt=create is enabled in identity server configuration:
        // https://docs.duendesoftware.com/identityserver/reference/options#userinteraction
        // by setting CreateAccountUrl 

        var response = await Bff.BrowserClient.GetAsync(Bff.Url("/bff/login?prompt=create"))
            .CheckHttpStatusCode();

        response.RequestMessage!.RequestUri!.ToString()
            .ShouldStartWith(IdentityServer.Url("/account/create").ToString());
        response.RequestMessage!.RequestUri!.ToString().ShouldNotContain("error");
    }

    [Theory]
    [MemberData(nameof(AllSetups))]
    public async Task login_endpoint_should_authenticatre_and_redirect_to_root(BffSetupType setup)
    {
        await ConfigureBff(setup);

        var response = await Bff.BrowserClient.Login();
        response.RequestMessage!.RequestUri.ShouldBe(Bff.Url("/"));
    }

    [Theory]
    [MemberData(nameof(AllSetups))]
    public async Task login_endpoint_should_challenge_and_redirect_to_root_with_custom_prefix(BffSetupType setup)
    {
        Bff.OnConfigureServices += svcs =>
        {
            svcs.Configure<BffOptions>(options => { options.ManagementBasePath = "/custom/bff"; });
        };
        await ConfigureBff(setup);


        await Bff.BrowserClient.Login(expectedStatusCode: HttpStatusCode.NotFound);

        var response = await Bff.BrowserClient.Login("/custom");
        response.RequestMessage!.RequestUri.ShouldBe(Bff.Url("/"));
    }

    [Theory]
    [MemberData(nameof(AllSetups))]
    public async Task login_endpoint_should_challenge_and_redirect_to_root_with_custom_prefix_trailing_slash(
        BffSetupType setup)
    {
        Bff.OnConfigureServices += svcs =>
        {
            svcs.Configure<BffOptions>(options => { options.ManagementBasePath = "/custom/bff/"; });
        };

        await ConfigureBff(setup);


        await Bff.BrowserClient.Login(expectedStatusCode: HttpStatusCode.NotFound);

        var response = await Bff.BrowserClient.Login("/custom");
        response.RequestMessage!.RequestUri.ShouldBe(Bff.Url("/"));
    }

    [Theory]
    [MemberData(nameof(AllSetups))]
    public async Task login_endpoint_should_challenge_and_redirect_to_root_with_root_prefix(BffSetupType setup)
    {
        Bff.OnConfigureServices += svcs =>
        {
            svcs.Configure<BffOptions>(options => { options.ManagementBasePath = "/"; });
        };

        await ConfigureBff(setup);

        var response = await Bff.BrowserClient.GetAsync(Bff.Url("/login"))
            .CheckHttpStatusCode();

        response.RequestMessage!.RequestUri.ShouldBe(Bff.Url("/"));
    }

    [Theory]
    [MemberData(nameof(AllSetups))]
    public async Task login_endpoint_with_existing_session_should_challenge(BffSetupType setup)
    {
        await ConfigureBff(setup);

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
        Bff.OnConfigureApp += app => app.MapGet("/foo", () => "foo'd you");
        await ConfigureBff(setup);

        var response = await Bff.BrowserClient.GetAsync(Bff.Url("/bff/login") + "?returnUrl=/foo")
            .CheckHttpStatusCode();

        var result = await response.Content.ReadAsStringAsync();
        result.ShouldBe("foo'd you");
    }

    [Theory]
    [MemberData(nameof(AllSetups))]
    public async Task login_endpoint_should_not_accept_non_local_returnUrl(BffSetupType setup)
    {
        await ConfigureBff(setup);

        var problem = await Bff.BrowserClient.GetAsync(Bff.Url("/bff/login") + "?returnUrl=https://foo")
            .ShouldBeProblem();

        problem.Errors.ShouldContainKey(Constants.RequestParameters.ReturnUrl);
    }

    // This test proves that split host functionality works.
    // https://github.com/DuendeSoftware/issues/issues/110
    [Theory]
    [MemberData(nameof(AllSetups))]
    public async Task given_list_of_referers_when_receiving_referer_on_silent_callback_then_allowed(BffSetupType setup)
    {
        await ConfigureBff(setup);
        Bff.BffOptions.AllowedSilentLoginReferers.Add("https://allowed.com");

        await Bff.BrowserClient.Login();

        Bff.BrowserClient.DefaultRequestHeaders.Add("Referer", "https://ALLOWED.com");

        var response = await Bff.BrowserClient.GetAsync(Bff.Url("/bff/login?prompt=none"))
            .CheckHttpStatusCode();

        response.Content.Headers.ContentType!.MediaType.ShouldBe("text/html");

        response.RequestMessage!.RequestUri.ShouldBe(Bff.Url("/bff/silent-login-callback"));

        var message = await response.Content.ReadAsStringAsync();
        message.ShouldContain("source:'bff-silent-login");
        message.ShouldContain($"isLoggedIn:true");
    }

    // This test guards against missing referer headers
    // https://github.com/DuendeSoftware/issues/issues/110
    [Theory]
    [MemberData(nameof(AllSetups))]
    public async Task given_list_of_referers_without_missing_header_then_returns_bad_request(BffSetupType setup)
    {
        await ConfigureBff(setup);
        Bff.BffOptions.AllowedSilentLoginReferers.Add("https://allowed.com");

        await Bff.BrowserClient.Login();

        await Bff.BrowserClient.GetAsync(Bff.Url("/bff/login?prompt=none"))
            .CheckHttpStatusCode(HttpStatusCode.BadRequest);
    }

    // This test guards against incorrect referer
    // https://github.com/DuendeSoftware/issues/issues/110
    [Theory]
    [MemberData(nameof(AllSetups))]
    public async Task given_list_of_referers_with_invalid_referer_then_returns_bad_request(BffSetupType setup)
    {
        await ConfigureBff(setup);
        Bff.BffOptions.AllowedSilentLoginReferers.Add("https://allowed.com");

        await Bff.BrowserClient.Login();

        Bff.BrowserClient.DefaultRequestHeaders.Add("Referer", "https://not_allowed.com");
        await Bff.BrowserClient.GetAsync(Bff.Url("/bff/login?prompt=none"))
            .CheckHttpStatusCode(HttpStatusCode.BadRequest);
    }
}
