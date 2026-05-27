// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using Duende.Bff.DynamicFrontends;
using Duende.Bff.Tests.TestInfra;
using Microsoft.Extensions.Options;

namespace Duende.Bff.Tests;

public class CookiePrefixValidationTests : BffTestBase
{
    [Fact]
    public async Task Host_prefix_with_valid_config_succeeds()
    {
        await InitializeAsync();
        AddOrUpdateFrontend(Some.BffFrontend() with
        {
            ConfigureOpenIdConnectOptions = The.DefaultOpenIdConnectConfiguration
        });

        var response = await Bff.BrowserClient.Login();
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Secure_prefix_with_custom_path_succeeds()
    {
        await InitializeAsync();
        AddOrUpdateFrontend(Some.BffFrontend() with
        {
            Name = BffFrontendName.Parse("path_frontend"),
            MatchingCriteria = new FrontendMatchingCriteria { MatchingPath = "/app" },
            ConfigureOpenIdConnectOptions = The.DefaultOpenIdConnectConfiguration
        });

        var response = await Bff.BrowserClient.Login("/app");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Host_prefix_with_domain_fails_validation()
    {
        await InitializeAsync();
        AddOrUpdateFrontend(Some.BffFrontend() with
        {
            ConfigureCookieOptions = opt => opt.Cookie.Domain = "example.com",
            ConfigureOpenIdConnectOptions = The.DefaultOpenIdConnectConfiguration
        });

        _ = await Bff.BrowserClient.Login()
            .ShouldThrowAsync<OptionsValidationException>();
    }

    [Fact]
    public async Task Host_prefix_with_non_root_path_fails_validation()
    {
        await InitializeAsync();
        AddOrUpdateFrontend(Some.BffFrontend() with
        {
            ConfigureCookieOptions = opt => opt.Cookie.Path = "/app",
            ConfigureOpenIdConnectOptions = The.DefaultOpenIdConnectConfiguration
        });

        _ = await Bff.BrowserClient.Login()
            .ShouldThrowAsync<OptionsValidationException>();
    }

    [Fact]
    public async Task Host_prefix_with_secure_policy_none_fails_validation()
    {
        await InitializeAsync();
        AddOrUpdateFrontend(Some.BffFrontend() with
        {
            ConfigureCookieOptions = opt => opt.Cookie.SecurePolicy = CookieSecurePolicy.None,
            ConfigureOpenIdConnectOptions = The.DefaultOpenIdConnectConfiguration
        });

        _ = await Bff.BrowserClient.Login()
            .ShouldThrowAsync<OptionsValidationException>();
    }

    [Fact]
    public async Task Secure_prefix_with_secure_policy_none_fails_validation()
    {
        await InitializeAsync();
        AddOrUpdateFrontend(Some.BffFrontend() with
        {
            Name = BffFrontendName.Parse("path_frontend"),
            MatchingCriteria = new FrontendMatchingCriteria { MatchingPath = "/app" },
            ConfigureCookieOptions = opt => opt.Cookie.SecurePolicy = CookieSecurePolicy.None,
            ConfigureOpenIdConnectOptions = The.DefaultOpenIdConnectConfiguration
        });

        _ = await Bff.BrowserClient.Login("/app")
            .ShouldThrowAsync<OptionsValidationException>();
    }
}
