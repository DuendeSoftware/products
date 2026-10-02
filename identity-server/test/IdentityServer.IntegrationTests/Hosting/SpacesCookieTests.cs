// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System.Security.Claims;
using Duende.IdentityServer.IntegrationTests.Common;
using Duende.IdentityServer.IntegrationTests.TestFramework;
using Duende.IdentityServer.IntegrationTests.TestFramework.TestIsolation;
using Duende.Spaces;
using Duende.Storage.Internal;
using Duende.Storage.Schema;
using Duende.Storage.Sqlite;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.IdentityServer.IntegrationTests.Hosting;

public sealed class SpacesCookieTests(WebServerFixture webServerFixture) : IAsyncLifetime
{
    private const string MainScheme = IdentityServerConstants.DefaultCookieAuthenticationScheme;
    private const string ExternalScheme = IdentityServerConstants.ExternalCookieAuthenticationScheme;

    private readonly Ct _ct = TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private sealed class ServerHandle : IAsyncDisposable
    {
        public required KestrelBasedTestServer Server { get; init; }
        public required ISpaceAdmin SpaceAdmin { get; init; }

        public async ValueTask DisposeAsync() => await Server.DisposeAsync();
    }

    private async Task<ServerHandle> CreateServerAsync(
        string name,
        Action<IServiceCollection>? configureServices = null,
        Action<Duende.IdentityServer.Configuration.IdentityServerOptions>? configureOptions = null,
        Action<IIdentityServerBuilder>? configureIdentityServer = null,
        Action<WebAppWrapper>? beforeSpaces = null,
        bool addServerSideSessions = false,
        bool fallbackToDefaultSpace = true,
        bool skipSpaceResolution = false)
    {
        var dbName = $"mscookie_{name}_{Guid.NewGuid():N}";
        var output = TestContext.Current.TestOutputHelper!;

        // The server name is embedded in the hostname (e.g. "{testId}-{serverName}.dev.localhost"),
        // so it must be short and free of characters IDN mapping rejects (like '_'). The full
        // test name is retained for the DB name and log prefix instead.
        var hostAlias = $"ms{Guid.NewGuid():N}"[..10];

        var server = new KestrelBasedTestServer(
            hostAlias,
            webServerFixture,
            new PrefixedTestOutputHelper(output, name),
            services =>
            {
                services.AddRouting();

                services.AddStorageInternal(storage =>
                    storage.AddSqliteInMemory(dbName));

                services.AddSpaces();
                services.Configure<SpacesOptions>(opt =>
                {
                    opt.FallbackToDefault = fallbackToDefaultSpace;
                });

                var isBuilder = services.AddIdentityServer(options =>
                {
                    options.KeyManagement.Enabled = false;
                    configureOptions?.Invoke(options);
                });

                isBuilder
                    .AddInMemoryClients([])
                    .AddInMemoryApiScopes([])
                    .AddInMemoryIdentityResources([]);

                if (addServerSideSessions)
                {
                    isBuilder.AddServerSideSessions();
                }

                configureIdentityServer?.Invoke(isBuilder);
                configureServices?.Invoke(services);
            },
            webapp =>
            {
                beforeSpaces?.Invoke(webapp);

                if (!skipSpaceResolution)
                {
                    webapp.UseSpaceResolution();
                }

                webapp.UseIdentityServer();

                // Terminal test-only handlers. Deliberately implemented as plain
                // middleware (not endpoint routing) since endpoint matching in
                // WebAppWrapper occurs via UseRouting() called before this
                // delegate runs, i.e. before space path rewrites would be visible.
                webapp.Use(async (ctx, next) => await HandleTestEndpointsAsync(ctx, next));
            });

        await server.StartAsync();

        var schema = server.GetRequiredService<IStorageInstanceSchema>();
        await schema.MigrateAsync(_ct);

        var spaceAdmin = server.GetRequiredService<ISpaceAdmin>();

        return new ServerHandle { Server = server, SpaceAdmin = spaceAdmin };
    }

    private static async Task HandleTestEndpointsAsync(HttpContext ctx, RequestDelegate next)
    {
        var path = ctx.Request.Path.Value ?? "";

        if (path.EndsWith("/test/signin", StringComparison.Ordinal))
        {
            var scheme = ctx.Request.Query["scheme"].FirstOrDefault() ?? MainScheme;
            var sub = ctx.Request.Query["sub"].FirstOrDefault() ?? "bob";
            var persistent = ctx.Request.Query["persistent"].FirstOrDefault() != "false";
            var principal = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim("sub", sub)], scheme, "sub", null));
            await ctx.SignInAsync(scheme, principal, new AuthenticationProperties { IsPersistent = persistent });
            ctx.Response.StatusCode = 200;
            return;
        }

        if (path.EndsWith("/test/whoami", StringComparison.Ordinal))
        {
            var scheme = ctx.Request.Query["scheme"].FirstOrDefault() ?? MainScheme;
            var result = await ctx.AuthenticateAsync(scheme);
            if (result.Succeeded)
            {
                ctx.Response.StatusCode = 200;
                await ctx.Response.WriteAsync(result.Principal!.FindFirst("sub")!.Value);
            }
            else
            {
                ctx.Response.StatusCode = 401;
            }
            return;
        }

        if (path.EndsWith("/test/signout", StringComparison.Ordinal))
        {
            var scheme = ctx.Request.Query["scheme"].FirstOrDefault() ?? MainScheme;
            await ctx.SignOutAsync(scheme);
            ctx.Response.StatusCode = 200;
            return;
        }

        await next(ctx);
    }

    private static HttpClient CreateBrowserClient(KestrelBasedTestServer server)
    {
#pragma warning disable CA2000 // Ownership transferred to HttpClient via disposeHandler: true
        var browserHandler = new BrowserHandler(server.CreateHandler(allowAutoRedirect: false))
        {
            AllowAutoRedirect = false
        };
#pragma warning restore CA2000
        return new HttpClient(browserHandler, disposeHandler: true) { BaseAddress = server.BaseAddress };
    }

    private static IEnumerable<string> SetCookieHeaders(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var values) ? values : [];

    [Fact]
    public async Task two_spaces_on_one_origin_isolate_authenticated_subjects_under_same_cookie_name()
    {
        await using var handle = await CreateServerAsync(nameof(two_spaces_on_one_origin_isolate_authenticated_subjects_under_same_cookie_name));

        await handle.SpaceAdmin.CreateAsync(
            new CreateSpaceConfiguration { Name = "Space A", MatchPatterns = [new SpaceMatchPattern { Path = "/space-a" }] }, _ct);
        await handle.SpaceAdmin.CreateAsync(
            new CreateSpaceConfiguration { Name = "Space B", MatchPatterns = [new SpaceMatchPattern { Path = "/space-b" }] }, _ct);

        using var client = CreateBrowserClient(handle.Server);

        var signInA = await client.GetAsync("/t/space-a/test/signin?sub=alice");
        signInA.EnsureSuccessStatusCode();
        var setCookieA = SetCookieHeaders(signInA).Single(h => h.StartsWith($"{MainScheme}=", StringComparison.Ordinal));
        setCookieA.ShouldContain("path=/t/space-a", Case.Insensitive);

        var signInB = await client.GetAsync("/t/space-b/test/signin?sub=bob");
        signInB.EnsureSuccessStatusCode();
        var setCookieB = SetCookieHeaders(signInB).Single(h => h.StartsWith($"{MainScheme}=", StringComparison.Ordinal));
        setCookieB.ShouldContain("path=/t/space-b", Case.Insensitive);

        // Same cookie name, different values scoped by path -> the container holds both.
        var whoAmIA = await client.GetAsync("/t/space-a/test/whoami");
        (await whoAmIA.Content.ReadAsStringAsync()).ShouldBe("alice");

        var whoAmIB = await client.GetAsync("/t/space-b/test/whoami");
        (await whoAmIB.Content.ReadAsStringAsync()).ShouldBe("bob");
    }

    [Fact]
    public async Task sliding_renewal_and_delete_use_resolved_space_path_and_logout_does_not_leak_across_spaces()
    {
        // A short, deterministically-controlled cookie lifetime lets us drive the real
        // CookieAuthenticationHandler's sliding-expiration renewal via a FakeTimeProvider,
        // instead of relying on wall-clock timing or asserting conditionally.
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);

        await using var handle = await CreateServerAsync(
            nameof(sliding_renewal_and_delete_use_resolved_space_path_and_logout_does_not_leak_across_spaces),
            configureServices: services =>
                services.Configure<CookieAuthenticationOptions>(MainScheme, opts => opts.TimeProvider = timeProvider),
            configureOptions: opts =>
            {
                opts.Authentication.CookieSlidingExpiration = true;
                opts.Authentication.CookieLifetime = TimeSpan.FromMinutes(20);
            });

        await handle.SpaceAdmin.CreateAsync(
            new CreateSpaceConfiguration { Name = "Space A", MatchPatterns = [new SpaceMatchPattern { Path = "/space-a" }] }, _ct);
        await handle.SpaceAdmin.CreateAsync(
            new CreateSpaceConfiguration { Name = "Space B", MatchPatterns = [new SpaceMatchPattern { Path = "/space-b" }] }, _ct);

        using var client = CreateBrowserClient(handle.Server);

        (await client.GetAsync("/t/space-a/test/signin?sub=alice")).EnsureSuccessStatusCode();
        (await client.GetAsync("/t/space-b/test/signin?sub=bob")).EnsureSuccessStatusCode();

        // Advance past the halfway point of the 20-minute lifetime (but before expiry) so the
        // CookieAuthenticationHandler's sliding-expiration check deterministically renews the ticket.
        timeProvider.Advance(TimeSpan.FromMinutes(15));

        // Sliding renewal: a whoami hit inside space A must reissue a cookie scoped to space A's path.
        var renewed = await client.GetAsync("/t/space-a/test/whoami");
        renewed.EnsureSuccessStatusCode();
        SetCookieHeaders(renewed).Single(h => h.StartsWith($"{MainScheme}=", StringComparison.Ordinal))
            .ShouldContain("path=/t/space-a", Case.Insensitive);

        // Sign out of space A only.
        var signOutA = await client.GetAsync("/t/space-a/test/signout");
        signOutA.EnsureSuccessStatusCode();
        var deleteCookie = SetCookieHeaders(signOutA).Single(h => h.StartsWith($"{MainScheme}=", StringComparison.Ordinal));
        deleteCookie.ShouldContain("path=/t/space-a", Case.Insensitive);

        (await client.GetAsync("/t/space-a/test/whoami")).StatusCode.ShouldBe(System.Net.HttpStatusCode.Unauthorized);

        // Space B remains authenticated.
        var stillB = await client.GetAsync("/t/space-b/test/whoami");
        stillB.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK);
        (await stillB.Content.ReadAsStringAsync()).ShouldBe("bob");
    }

    [Fact]
    public async Task external_cookie_scheme_is_also_isolated_per_space()
    {
        await using var handle = await CreateServerAsync(nameof(external_cookie_scheme_is_also_isolated_per_space));

        await handle.SpaceAdmin.CreateAsync(
            new CreateSpaceConfiguration { Name = "Space A", MatchPatterns = [new SpaceMatchPattern { Path = "/space-a" }] }, _ct);
        await handle.SpaceAdmin.CreateAsync(
            new CreateSpaceConfiguration { Name = "Space B", MatchPatterns = [new SpaceMatchPattern { Path = "/space-b" }] }, _ct);

        using var client = CreateBrowserClient(handle.Server);

        var signInA = await client.GetAsync($"/t/space-a/test/signin?sub=alice&scheme={ExternalScheme}");
        signInA.EnsureSuccessStatusCode();
        SetCookieHeaders(signInA).Single(h => h.StartsWith($"{ExternalScheme}=", StringComparison.Ordinal))
            .ShouldContain("path=/t/space-a", Case.Insensitive);

        var whoAmIA = await client.GetAsync($"/t/space-a/test/whoami?scheme={ExternalScheme}");
        (await whoAmIA.Content.ReadAsStringAsync()).ShouldBe("alice");

        // The external cookie was never issued for space B.
        var whoAmIB = await client.GetAsync($"/t/space-b/test/whoami?scheme={ExternalScheme}");
        whoAmIB.StatusCode.ShouldBe(System.Net.HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task origin_only_space_resolves_cookie_path_at_root_without_path_prefix()
    {
        await using var handle = await CreateServerAsync(nameof(origin_only_space_resolves_cookie_path_at_root_without_path_prefix));

        var originHost = $"origin-only-{handle.Server.TestId}.dev.localhost";
        handle.Server.RegisterHostAlias(originHost);

        await handle.SpaceAdmin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "Origin Space",
                MatchPatterns = [new SpaceMatchPattern { Origin = $"https://{originHost}:{handle.Server.BaseAddress.Port}" }]
            },
            _ct);

        using var client = CreateBrowserClient(handle.Server);

        var uri = new Uri($"https://{originHost}:{handle.Server.BaseAddress.Port}/test/signin?sub=alice");
        var response = await client.GetAsync(uri);
        response.EnsureSuccessStatusCode();

        // No path rewrite occurs for an origin-only match, so the resolved cookie path is root.
        SetCookieHeaders(response).Single(h => h.StartsWith($"{MainScheme}=", StringComparison.Ordinal))
            .ShouldContain("path=/", Case.Insensitive);
    }

    [Fact]
    public async Task root_single_space_behavior_uses_root_cookie_path()
    {
        await using var handle = await CreateServerAsync(
            nameof(root_single_space_behavior_uses_root_cookie_path),
            fallbackToDefaultSpace: true);

        // No spaces registered at all -> everything falls back to the default (root) space.
        using var client = CreateBrowserClient(handle.Server);

        var response = await client.GetAsync("/test/signin?sub=alice");
        response.EnsureSuccessStatusCode();

        SetCookieHeaders(response).Single(h => h.StartsWith($"{MainScheme}=", StringComparison.Ordinal))
            .ShouldContain("path=/", Case.Insensitive);
    }

    [Fact]
    public async Task ordinary_hosting_pathbase_composes_with_space_pathbase()
    {
        await using var handle = await CreateServerAsync(
            nameof(ordinary_hosting_pathbase_composes_with_space_pathbase),
            beforeSpaces: webapp => webapp.UsePathBase("/hosted"));

        await handle.SpaceAdmin.CreateAsync(
            new CreateSpaceConfiguration { Name = "Space A", MatchPatterns = [new SpaceMatchPattern { Path = "/space-a" }] }, _ct);

        using var client = CreateBrowserClient(handle.Server);

        var response = await client.GetAsync("/hosted/t/space-a/test/signin?sub=alice");
        response.EnsureSuccessStatusCode();

        SetCookieHeaders(response).Single(h => h.StartsWith($"{MainScheme}=", StringComparison.Ordinal))
            .ShouldContain("path=/hosted/t/space-a", Case.Insensitive);
    }

    [Fact]
    public async Task hosting_pathbase_alone_without_space_match_resolves_to_hosting_base()
    {
        await using var handle = await CreateServerAsync(
            nameof(hosting_pathbase_alone_without_space_match_resolves_to_hosting_base),
            beforeSpaces: webapp => webapp.UsePathBase("/hosted"));

        using var client = CreateBrowserClient(handle.Server);

        var response = await client.GetAsync("/hosted/test/signin?sub=alice");
        response.EnsureSuccessStatusCode();

        SetCookieHeaders(response).Single(h => h.StartsWith($"{MainScheme}=", StringComparison.Ordinal))
            .ShouldContain("path=/hosted", Case.Insensitive);
    }

    [Fact]
    public async Task configured_domain_is_retained_alongside_space_resolved_path()
    {
        var domain = $"dev.localhost";
        await using var handle = await CreateServerAsync(
            nameof(configured_domain_is_retained_alongside_space_resolved_path),
            configureServices: services =>
                services.PostConfigure<CookieAuthenticationOptions>(MainScheme, opts => opts.Cookie.Domain = domain));

        await handle.SpaceAdmin.CreateAsync(
            new CreateSpaceConfiguration { Name = "Space A", MatchPatterns = [new SpaceMatchPattern { Path = "/space-a" }] }, _ct);

        using var client = CreateBrowserClient(handle.Server);

        var response = await client.GetAsync("/t/space-a/test/signin?sub=alice");
        response.EnsureSuccessStatusCode();

        var setCookie = SetCookieHeaders(response).Single(h => h.StartsWith($"{MainScheme}=", StringComparison.Ordinal));
        setCookie.ShouldContain("path=/t/space-a", Case.Insensitive);
        setCookie.ShouldContain($"domain={domain}", Case.Insensitive);
    }

    [Fact]
    public async Task custom_selected_scheme_is_wrapped_while_unrelated_scheme_is_unaffected()
    {
        const string customScheme = "custom-idsrv-cookie";
        const string unrelatedScheme = "unrelated-cookie";

        await using var handle = await CreateServerAsync(
            nameof(custom_selected_scheme_is_wrapped_while_unrelated_scheme_is_unaffected),
            configureServices: services =>
            {
                services
                    .AddAuthentication()
                    .AddCookie(customScheme, opts => opts.Cookie.Name = customScheme)
                    .AddCookie(unrelatedScheme, opts =>
                    {
                        opts.Cookie.Name = unrelatedScheme;
                        opts.Cookie.Path = "/fixed-unrelated-path";
                    });
            },
            configureOptions: opts => opts.Authentication.CookieAuthenticationScheme = customScheme);

        await handle.SpaceAdmin.CreateAsync(
            new CreateSpaceConfiguration { Name = "Space A", MatchPatterns = [new SpaceMatchPattern { Path = "/space-a" }] }, _ct);

        using var client = CreateBrowserClient(handle.Server);

        var signInCustom = await client.GetAsync($"/t/space-a/test/signin?sub=alice&scheme={customScheme}");
        signInCustom.EnsureSuccessStatusCode();
        SetCookieHeaders(signInCustom).Single(h => h.StartsWith($"{customScheme}=", StringComparison.Ordinal))
            .ShouldContain("path=/t/space-a", Case.Insensitive);

        var signInUnrelated = await client.GetAsync($"/t/space-a/test/signin?sub=alice&scheme={unrelatedScheme}");
        signInUnrelated.EnsureSuccessStatusCode();
        SetCookieHeaders(signInUnrelated).Single(h => h.StartsWith($"{unrelatedScheme}=", StringComparison.Ordinal))
            .ShouldContain("path=/fixed-unrelated-path", Case.Insensitive);
    }

    [Fact]
    public async Task default_application_cookie_scheme_is_wrapped_when_selected_as_default_scheme()
    {
        const string appScheme = "Identity.Application";

        await using var handle = await CreateServerAsync(
            nameof(default_application_cookie_scheme_is_wrapped_when_selected_as_default_scheme),
            configureServices: services =>
            {
                services
                    .AddAuthentication(opts =>
                    {
                        opts.DefaultScheme = appScheme;
                        opts.DefaultAuthenticateScheme = appScheme;
                        opts.DefaultSignInScheme = appScheme;
                    })
                    .AddCookie(appScheme, opts => opts.Cookie.Name = appScheme);
            });

        await handle.SpaceAdmin.CreateAsync(
            new CreateSpaceConfiguration { Name = "Space A", MatchPatterns = [new SpaceMatchPattern { Path = "/space-a" }] }, _ct);

        using var client = CreateBrowserClient(handle.Server);

        var signIn = await client.GetAsync($"/t/space-a/test/signin?sub=alice&scheme={appScheme}");
        signIn.EnsureSuccessStatusCode();
        SetCookieHeaders(signIn).Single(h => h.StartsWith($"{appScheme}=", StringComparison.Ordinal))
            .ShouldContain("path=/t/space-a", Case.Insensitive);
    }

    [Fact]
    public async Task server_side_sessions_authenticate_renew_and_signout_independently_per_space()
    {
        // As above: a controllable TimeProvider drives real sliding-expiration renewal through
        // the CookieAuthenticationHandler, this time with server-side sessions enabled, so we
        // assert an actual reissued Set-Cookie header rather than only an authenticate result.
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);

        await using var handle = await CreateServerAsync(
            nameof(server_side_sessions_authenticate_renew_and_signout_independently_per_space),
            configureServices: services =>
                services.Configure<CookieAuthenticationOptions>(MainScheme, opts => opts.TimeProvider = timeProvider),
            configureOptions: opts =>
            {
                opts.Authentication.CookieSlidingExpiration = true;
                opts.Authentication.CookieLifetime = TimeSpan.FromMinutes(20);
            },
            addServerSideSessions: true);

        await handle.SpaceAdmin.CreateAsync(
            new CreateSpaceConfiguration { Name = "Space A", MatchPatterns = [new SpaceMatchPattern { Path = "/space-a" }] }, _ct);
        await handle.SpaceAdmin.CreateAsync(
            new CreateSpaceConfiguration { Name = "Space B", MatchPatterns = [new SpaceMatchPattern { Path = "/space-b" }] }, _ct);

        using var client = CreateBrowserClient(handle.Server);

        (await client.GetAsync("/t/space-a/test/signin?sub=alice")).EnsureSuccessStatusCode();
        (await client.GetAsync("/t/space-b/test/signin?sub=bob")).EnsureSuccessStatusCode();

        var whoAmIA = await client.GetAsync("/t/space-a/test/whoami");
        whoAmIA.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK);
        (await whoAmIA.Content.ReadAsStringAsync()).ShouldBe("alice");

        var whoAmIB = await client.GetAsync("/t/space-b/test/whoami");
        whoAmIB.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK);
        (await whoAmIB.Content.ReadAsStringAsync()).ShouldBe("bob");

        // Advance past the halfway point of the 20-minute lifetime (but before expiry) so the
        // CookieAuthenticationHandler's sliding-expiration check deterministically renews the
        // server-side-session-backed ticket, reissuing a cookie scoped to space A's path.
        timeProvider.Advance(TimeSpan.FromMinutes(15));

        var renewedA = await client.GetAsync("/t/space-a/test/whoami");
        renewedA.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK);
        (await renewedA.Content.ReadAsStringAsync()).ShouldBe("alice");
        SetCookieHeaders(renewedA).Single(h => h.StartsWith($"{MainScheme}=", StringComparison.Ordinal))
            .ShouldContain("path=/t/space-a", Case.Insensitive);

        // Sign out of A only; B keeps its independent server-side session.
        (await client.GetAsync("/t/space-a/test/signout")).EnsureSuccessStatusCode();

        (await client.GetAsync("/t/space-a/test/whoami")).StatusCode.ShouldBe(System.Net.HttpStatusCode.Unauthorized);

        var stillB = await client.GetAsync("/t/space-b/test/whoami");
        stillB.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK);
        (await stillB.Content.ReadAsStringAsync()).ShouldBe("bob");
    }

    [Fact]
    public async Task all_identityserver_owned_cookies_share_the_resolved_space_base_path()
    {
        // Sign-in triggers both the main auth cookie (via CookieAuthenticationHandler) and
        // the check-session cookie (idsrv.session, via DefaultUserSession.CreateSessionIdAsync,
        // hooked in by IdentityServerAuthenticationService.SignInAsync) in the same response.
        // Both are expected to be decorated with the same resolved Spaces base path.
        await using var handle = await CreateServerAsync(
            nameof(all_identityserver_owned_cookies_share_the_resolved_space_base_path),
            beforeSpaces: webapp => webapp.UsePathBase("/hosted"));

        await handle.SpaceAdmin.CreateAsync(
            new CreateSpaceConfiguration { Name = "Space A", MatchPatterns = [new SpaceMatchPattern { Path = "/space-a" }] }, _ct);

        using var client = CreateBrowserClient(handle.Server);

        var signIn = await client.GetAsync("/hosted/t/space-a/test/signin?sub=alice");
        signIn.EnsureSuccessStatusCode();

        var setCookieHeaders = SetCookieHeaders(signIn).ToList();

        var mainCookie = setCookieHeaders.SingleOrDefault(h => h.StartsWith($"{MainScheme}=", StringComparison.Ordinal));
        mainCookie.ShouldNotBeNull();
        mainCookie.ShouldContain("path=/hosted/t/space-a", Case.Insensitive);

        var checkSessionCookie = setCookieHeaders.SingleOrDefault(h => h.StartsWith("idsrv.session=", StringComparison.Ordinal));
        checkSessionCookie.ShouldNotBeNull();
        checkSessionCookie.ShouldContain("path=/hosted/t/space-a", Case.Insensitive);

        // Sanity: both cookies resolve to the exact same path string, not just a prefix match.
        var mainPath = mainCookie.Split(';').Select(p => p.Trim()).Single(p => p.StartsWith("path=", StringComparison.OrdinalIgnoreCase));
        var checkSessionPath = checkSessionCookie.Split(';').Select(p => p.Trim()).Single(p => p.StartsWith("path=", StringComparison.OrdinalIgnoreCase));
        mainPath.Equals(checkSessionPath, StringComparison.OrdinalIgnoreCase).ShouldBeTrue();

        // Note: this server has no OIDC external authentication configured, so there is no
        // correlation/nonce cookie in play here; that deliberately-narrower-path behavior is
        // covered separately in SpacesExternalOidcCookieTests.
    }

    [Fact]
    public async Task missing_space_resolution_middleware_fails_closed_to_root_cookie_path()
    {
        // Warp security finding W-2: if UseSpaceResolution() is omitted (or misordered)
        // relative to UseIdentityServer(), the resolved cookie path must fail closed to root
        // rather than leaking a wrong/stale space path. A space is still registered here to
        // prove that, without the resolution middleware installed, its path is never picked up.
        await using var handle = await CreateServerAsync(
            nameof(missing_space_resolution_middleware_fails_closed_to_root_cookie_path),
            skipSpaceResolution: true);

        await handle.SpaceAdmin.CreateAsync(
            new CreateSpaceConfiguration { Name = "Space A", MatchPatterns = [new SpaceMatchPattern { Path = "/space-a" }] }, _ct);

        using var client = CreateBrowserClient(handle.Server);

        // A request to a URL that would normally match Space A's path, but with no resolution
        // middleware in the pipeline to rewrite it.
        var response = await client.GetAsync("/t/space-a/test/signin?sub=alice");
        response.EnsureSuccessStatusCode();

        // No isolation is expected here, but the cookie must never leak a stale/wrong space
        // path -- it must fail closed to root.
        SetCookieHeaders(response).Single(h => h.StartsWith($"{MainScheme}=", StringComparison.Ordinal))
            .ShouldContain("path=/", Case.Insensitive);
    }
}
