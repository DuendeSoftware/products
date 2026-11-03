// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.Configuration;
using Duende.Bff.DynamicFrontends;
using Duende.Bff.DynamicFrontends.Internal;
using Duende.Bff.Tests.TestInfra;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Options;

namespace Duende.Bff.Tests.DynamicFrontends;

public class BffAuthenticationSchemeProviderTests
{
    private readonly BffFrontend _frontend = new(BffFrontendName.Parse("some-frontend"));

    [Fact]
    public async Task GetDefaultAuthenticateSchemeAsync_with_frontend_selected_returns_frontend_cookie_scheme()
    {
        var sut = CreateSut(_frontend, new AuthenticationOptions { DefaultAuthenticateScheme = "Bearer" });

        var scheme = await sut.GetDefaultAuthenticateSchemeAsync();

        _ = scheme.ShouldNotBeNull();
        scheme.Name.ShouldBe(_frontend.CookieSchemeName.ToString());
        scheme.HandlerType.ShouldBe(typeof(CookieAuthenticationHandler));
    }

    [Fact]
    public async Task GetDefaultSignInSchemeAsync_with_frontend_selected_returns_frontend_cookie_scheme()
    {
        var sut = CreateSut(_frontend, new AuthenticationOptions { DefaultSignInScheme = "Bearer" });

        var scheme = await sut.GetDefaultSignInSchemeAsync();

        _ = scheme.ShouldNotBeNull();
        scheme.Name.ShouldBe(_frontend.CookieSchemeName.ToString());
        scheme.HandlerType.ShouldBe(typeof(CookieAuthenticationHandler));
    }

    [Fact]
    public async Task GetDefaultForbidSchemeAsync_with_frontend_selected_returns_frontend_cookie_scheme()
    {
        var sut = CreateSut(_frontend, new AuthenticationOptions { DefaultForbidScheme = "Bearer" });

        var scheme = await sut.GetDefaultForbidSchemeAsync();

        _ = scheme.ShouldNotBeNull();
        scheme.Name.ShouldBe(_frontend.CookieSchemeName.ToString());
        scheme.HandlerType.ShouldBe(typeof(CookieAuthenticationHandler));
    }

    [Fact]
    public async Task GetDefaultChallengeSchemeAsync_with_frontend_selected_returns_frontend_oidc_scheme()
    {
        var sut = CreateSut(_frontend, new AuthenticationOptions { DefaultChallengeScheme = "Bearer" });

        var scheme = await sut.GetDefaultChallengeSchemeAsync();

        _ = scheme.ShouldNotBeNull();
        scheme.Name.ShouldBe(_frontend.OidcSchemeName.ToString());
        scheme.HandlerType.ShouldBe(typeof(OpenIdConnectHandler));
    }

    [Fact]
    public async Task GetDefaultSignOutSchemeAsync_with_frontend_selected_returns_frontend_oidc_scheme()
    {
        var sut = CreateSut(_frontend, new AuthenticationOptions { DefaultSignOutScheme = "Bearer" });

        var scheme = await sut.GetDefaultSignOutSchemeAsync();

        _ = scheme.ShouldNotBeNull();
        scheme.Name.ShouldBe(_frontend.OidcSchemeName.ToString());
        scheme.HandlerType.ShouldBe(typeof(OpenIdConnectHandler));
    }

    [Fact]
    public async Task GetDefaultAuthenticateSchemeAsync_with_no_frontend_selected_defers_to_host_configured_default()
    {
        var options = new AuthenticationOptions { DefaultAuthenticateScheme = "Bearer" };
        var sut = CreateSut(frontend: null, options);
        AddBearerScheme(sut);

        var scheme = await sut.GetDefaultAuthenticateSchemeAsync();

        _ = scheme.ShouldNotBeNull();
        scheme.Name.ShouldBe("Bearer");
    }

    [Fact]
    public async Task GetDefaultChallengeSchemeAsync_with_no_http_context_defers_to_host_configured_default()
    {
        var options = new AuthenticationOptions { DefaultChallengeScheme = "Bearer" };
        var sut = CreateSut(frontend: _frontend, options, includeHttpContext: false);
        AddBearerScheme(sut);

        var scheme = await sut.GetDefaultChallengeSchemeAsync();

        _ = scheme.ShouldNotBeNull();
        scheme.Name.ShouldBe("Bearer");
    }

    private static void AddBearerScheme(BffAuthenticationSchemeProvider sut) =>
        sut.AddScheme(new AuthenticationScheme("Bearer", "Bearer", typeof(TestBearerHandler)));

    private static BffAuthenticationSchemeProvider CreateSut(BffFrontend? frontend, AuthenticationOptions options, bool includeHttpContext = true)
    {
        var contextAccessor = new FakeHttpContextAccessor();

        var frontendAccessor = new CurrentFrontendAccessor(contextAccessor);
        if (frontend != null)
        {
            frontendAccessor.Set(frontend);
        }

        if (!includeHttpContext)
        {
            contextAccessor.HttpContext = null;
        }

        return new BffAuthenticationSchemeProvider(
            contextAccessor,
            frontendAccessor,
            Options.Create(options),
            Options.Create(new BffOptions()));
    }

    private sealed class TestBearerHandler : IAuthenticationHandler
    {
        public Task InitializeAsync(AuthenticationScheme scheme, HttpContext context) => Task.CompletedTask;
        public Task<AuthenticateResult> AuthenticateAsync() => Task.FromResult(AuthenticateResult.NoResult());
        public Task ChallengeAsync(AuthenticationProperties? properties) => Task.CompletedTask;
        public Task ForbidAsync(AuthenticationProperties? properties) => Task.CompletedTask;
    }
}

