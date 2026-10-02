// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.IntegrationTests.Common;
using Duende.IdentityServer.IntegrationTests.TestFramework;
using Duende.IdentityServer.IntegrationTests.TestFramework.TestIsolation;
using Duende.IdentityServer.Models;
using Duende.Spaces;
using Duende.Storage.Internal;
using Duende.Storage.Schema;
using Duende.Storage.Sqlite;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.IdentityServer.IntegrationTests.Hosting;

public sealed class SpacesExternalOidcCookieTests(WebServerFixture webServerFixture) : IAsyncLifetime
{
    private const string MainScheme = IdentityServerConstants.DefaultCookieAuthenticationScheme;
    private const string ExternalScheme = IdentityServerConstants.ExternalCookieAuthenticationScheme;
    private const string OidcScheme = "external-oidc";
    private const string ClientId = "external-client";
    private const string ClientSecret = "secret";

    private readonly Ct _ct = TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private sealed class ExternalOidcFixture : IAsyncDisposable
    {
        public required KestrelBasedTestServer MainServer { get; init; }
        public required KestrelBasedTestServer IdpServer { get; init; }
        public required ISpaceAdmin SpaceAdmin { get; init; }

        public async ValueTask DisposeAsync()
        {
            await MainServer.DisposeAsync();
            await IdpServer.DisposeAsync();
        }
    }

    private async Task<ExternalOidcFixture> CreateFixtureAsync(string name)
    {
        var dbName = $"msoidc_{name}_{Guid.NewGuid():N}";
        var output = TestContext.Current.TestOutputHelper!;

        var mainHostAlias = $"ms{Guid.NewGuid():N}"[..10];
        var idpHostAlias = $"idp{Guid.NewGuid():N}"[..10];

        // Forward-referenced: the main server's OIDC options need the IdP's base address,
        // but the IdP's client registration needs the main server's base address. Both
        // KestrelBasedTestServer.BaseAddress values are available immediately at
        // construction (before StartAsync), so this local variable only needs to be
        // assigned before the main server's configureServices delegate actually runs
        // (i.e. before StartAsync is called on it), not before it is constructed.
        KestrelBasedTestServer? idpServer = null;

        var mainServer = new KestrelBasedTestServer(
            mainHostAlias,
            webServerFixture,
            new PrefixedTestOutputHelper(output, name + "-main"),
            services =>
            {
                services.AddRouting();

                services.AddStorageInternal(storage =>
                    storage.AddSqliteInMemory(dbName));

                services.AddSpaces();

                services.AddIdentityServer(options => options.KeyManagement.Enabled = false)
                    .AddInMemoryClients([])
                    .AddInMemoryApiScopes([])
                    .AddInMemoryIdentityResources([]);

                services.AddAuthentication()
                    .AddOpenIdConnect(OidcScheme, options =>
                    {
                        options.SignInScheme = ExternalScheme;
                        options.Authority = idpServer!.BaseAddress.ToString().TrimEnd('/');
                        options.ClientId = ClientId;
                        options.ClientSecret = ClientSecret;
                        options.CallbackPath = $"/signin-{OidcScheme}";
                        options.ResponseType = "code";
                        options.ResponseMode = "query";
                        options.Scope.Clear();
                        options.Scope.Add("openid");
                        options.MapInboundClaims = false;
                        options.SaveTokens = false;
                        options.BackchannelHttpHandler = idpServer!.CreateHandler();
                    });
            },
            webapp =>
            {
                webapp.UseSpaceResolution();
                webapp.UseIdentityServer();

                // Terminal test-only handlers, implemented as plain middleware (not
                // endpoint routing) for the same reason documented in SpacesCookieTests:
                // endpoint matching occurs before space path rewrites would be visible.
                webapp.Use(async (ctx, next) => await HandleTestEndpointsAsync(ctx, next));
            });

        idpServer = new KestrelBasedTestServer(
            idpHostAlias,
            webServerFixture,
            new PrefixedTestOutputHelper(output, name + "-idp"),
            services =>
            {
                services.AddRouting();
                services.AddAuthorization();

                services.AddIdentityServer(options => options.KeyManagement.Enabled = false)
                    .AddInMemoryClients([
                        new Client
                        {
                            ClientId = ClientId,
                            ClientSecrets = { new Secret(ClientSecret.Sha256()) },
                            AllowedGrantTypes = GrantTypes.Code,
                            RequireConsent = false,
                            RedirectUris =
                            {
                                mainServer.BuildUrl($"/t/space-a/signin-{OidcScheme}").ToString(),
                                mainServer.BuildUrl($"/t/space-b/signin-{OidcScheme}").ToString()
                            },
                            AllowedScopes = { "openid" }
                        }
                    ])
                    .AddInMemoryIdentityResources([new IdentityResources.OpenId()])
                    .AddDeveloperSigningCredential(persistKey: false);
            },
            webapp =>
            {
                webapp.UseIdentityServer();

                webapp.MapGet("/login", async ctx =>
                {
                    var sub = ctx.Request.Query["sub"].FirstOrDefault() ?? "external-user";
                    await ctx.SignInAsync(new IdentityServerUser(sub).CreatePrincipal());
                });
            });

        await idpServer.StartAsync();
        await mainServer.StartAsync();

        var schema = mainServer.GetRequiredService<IStorageInstanceSchema>();
        await schema.MigrateAsync(_ct);

        var spaceAdmin = mainServer.GetRequiredService<ISpaceAdmin>();

        return new ExternalOidcFixture { MainServer = mainServer, IdpServer = idpServer, SpaceAdmin = spaceAdmin };
    }

    private static async Task HandleTestEndpointsAsync(HttpContext ctx, RequestDelegate next)
    {
        var path = ctx.Request.Path.Value ?? "";

        if (path == "/test/challenge")
        {
            var scheme = ctx.Request.Query["scheme"].FirstOrDefault() ?? OidcScheme;
            await ctx.ChallengeAsync(scheme,
                new AuthenticationProperties { RedirectUri = ctx.Request.PathBase + "/test/finish" });
            return;
        }

        if (path == "/test/finish")
        {
            var external = await ctx.AuthenticateAsync(ExternalScheme);
            if (external.Succeeded)
            {
                await ctx.SignInAsync(MainScheme, external.Principal!, external.Properties);
                await ctx.SignOutAsync(ExternalScheme);
                ctx.Response.StatusCode = 200;
                await ctx.Response.WriteAsync(external.Principal!.FindFirst("sub")!.Value);
            }
            else
            {
                ctx.Response.StatusCode = 401;
            }
            return;
        }

        if (path == "/test/whoami")
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

    private static async Task<(HttpResponseMessage Challenge, HttpResponseMessage IdpAuthorize, HttpResponseMessage Callback, HttpResponseMessage Finish)>
        RunExternalLoginAsync(HttpClient mainClient, HttpClient idpClient, string spacePath, string sub)
    {
        var challenge = await mainClient.GetAsync($"{spacePath}/test/challenge?scheme={OidcScheme}");
        challenge.StatusCode.ShouldBe(System.Net.HttpStatusCode.Redirect);
        var authorizeUrl = challenge.Headers.Location!.ToString();

        (await idpClient.GetAsync($"/login?sub={sub}")).EnsureSuccessStatusCode();
        var idpAuthorize = await idpClient.GetAsync(authorizeUrl);
        idpAuthorize.StatusCode.ShouldBe(System.Net.HttpStatusCode.SeeOther);
        var redirectUri = idpAuthorize.Headers.Location!.ToString();

        var callback = await mainClient.GetAsync(redirectUri);
        callback.StatusCode.ShouldBe(System.Net.HttpStatusCode.Redirect);
        var finishUri = callback.Headers.Location!.ToString();

        var finish = await mainClient.GetAsync(finishUri);

        return (challenge, idpAuthorize, callback, finish);
    }

    [Fact]
    public async Task external_login_completes_under_space_path_and_scopes_idsrv_cookies_while_leaving_oidc_cookies_at_framework_default()
    {
        await using var fixture = await CreateFixtureAsync(
            nameof(external_login_completes_under_space_path_and_scopes_idsrv_cookies_while_leaving_oidc_cookies_at_framework_default));

        await fixture.SpaceAdmin.CreateAsync(
            new CreateSpaceConfiguration { Name = "Space A", MatchPatterns = [new SpaceMatchPattern { Path = "/space-a" }] }, _ct);

        using var mainClient = CreateBrowserClient(fixture.MainServer);
        using var idpClient = CreateBrowserClient(fixture.IdpServer);

        var (challenge, _, callback, finish) =
            await RunExternalLoginAsync(mainClient, idpClient, "/t/space-a", "alice");

        // The challenge response sets the OIDC handler's own correlation and nonce cookies.
        // These are NOT decorated by IdentityServer: their Path must be the framework
        // default of PathBase + CallbackPath ("/t/space-a/signin-external-oidc"), not the
        // broader resolved IdentityServer cookie path ("/t/space-a").
        var challengeCookies = SetCookieHeaders(challenge).ToList();
        var correlationCookie = challengeCookies.Single(h => h.Contains(".AspNetCore.Correlation.", StringComparison.Ordinal));
        correlationCookie.ShouldContain("path=/t/space-a/signin-external-oidc", Case.Insensitive);

        var nonceCookie = challengeCookies.Single(h => h.Contains(".AspNetCore.OpenIdConnect.Nonce.", StringComparison.Ordinal));
        nonceCookie.ShouldContain("path=/t/space-a/signin-external-oidc", Case.Insensitive);

        // The callback response (processed automatically by the OIDC handler at
        // CallbackPath) signs into the external cookie scheme, which IS decorated by
        // IdentityServer: its Path must be the resolved space base path ("/t/space-a"),
        // not the narrower callback-specific path used by the OIDC handler's own cookies.
        var callbackCookies = SetCookieHeaders(callback).ToList();
        var externalCookie = callbackCookies.Single(h => h.StartsWith($"{ExternalScheme}=", StringComparison.Ordinal));
        externalCookie.ShouldContain("path=/t/space-a", Case.Insensitive);
        externalCookie.ShouldNotContain("path=/t/space-a/signin-external-oidc", Case.Insensitive);

        // The finish endpoint creates the main session cookie (also resolved-path-scoped)
        // and deletes the temporary external cookie (deletion Set-Cookie also resolved-path-scoped).
        finish.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK);
        (await finish.Content.ReadAsStringAsync()).ShouldBe("alice");

        var finishCookies = SetCookieHeaders(finish).ToList();
        var mainCookie = finishCookies.Single(h => h.StartsWith($"{MainScheme}=", StringComparison.Ordinal));
        mainCookie.ShouldContain("path=/t/space-a", Case.Insensitive);

        var deletedExternalCookie = finishCookies.Single(h => h.StartsWith($"{ExternalScheme}=", StringComparison.Ordinal));
        deletedExternalCookie.ShouldContain("path=/t/space-a", Case.Insensitive);

        // The temporary external cookie was consumed and removed; the main session persists.
        var whoAmI = await mainClient.GetAsync("/t/space-a/test/whoami");
        (await whoAmI.Content.ReadAsStringAsync()).ShouldBe("alice");

        var externalWhoAmI = await mainClient.GetAsync($"/t/space-a/test/whoami?scheme={ExternalScheme}");
        externalWhoAmI.StatusCode.ShouldBe(System.Net.HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task external_login_and_temporary_external_cookie_are_isolated_between_two_spaces()
    {
        await using var fixture = await CreateFixtureAsync(
            nameof(external_login_and_temporary_external_cookie_are_isolated_between_two_spaces));

        await fixture.SpaceAdmin.CreateAsync(
            new CreateSpaceConfiguration { Name = "Space A", MatchPatterns = [new SpaceMatchPattern { Path = "/space-a" }] }, _ct);
        await fixture.SpaceAdmin.CreateAsync(
            new CreateSpaceConfiguration { Name = "Space B", MatchPatterns = [new SpaceMatchPattern { Path = "/space-b" }] }, _ct);

        using var mainClient = CreateBrowserClient(fixture.MainServer);
        using var idpClient = CreateBrowserClient(fixture.IdpServer);

        // Drive the flow for space A up to (but not through) the finish step, so the
        // temporary external cookie for space A is present in the container while we
        // probe space B.
        var challengeA = await mainClient.GetAsync("/t/space-a/test/challenge?scheme=external-oidc");
        var authorizeUrlA = challengeA.Headers.Location!.ToString();
        (await idpClient.GetAsync("/login?sub=alice")).EnsureSuccessStatusCode();
        var idpAuthorizeA = await idpClient.GetAsync(authorizeUrlA);
        var callbackA = await mainClient.GetAsync(idpAuthorizeA.Headers.Location!.ToString());
        callbackA.StatusCode.ShouldBe(System.Net.HttpStatusCode.Redirect);

        SetCookieHeaders(callbackA).Single(h => h.StartsWith($"{ExternalScheme}=", StringComparison.Ordinal))
            .ShouldContain("path=/t/space-a", Case.Insensitive);

        // Space A's external cookie must not be visible under space B: the browser's real
        // CookieContainer only attaches it to requests matching its Path, so a whoami
        // check scoped to space B must fail.
        var crossSpaceWhoAmI = await mainClient.GetAsync($"/t/space-b/test/whoami?scheme={ExternalScheme}");
        crossSpaceWhoAmI.StatusCode.ShouldBe(System.Net.HttpStatusCode.Unauthorized);

        // Finish space A's login.
        var finishA = await mainClient.GetAsync(callbackA.Headers.Location!.ToString());
        finishA.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK);
        (await finishA.Content.ReadAsStringAsync()).ShouldBe("alice");

        // Now drive an independent full round trip for space B and confirm it completes
        // and produces its own, independently-scoped main and external cookies.
        var (_, _, callbackB, finishB) =
            await RunExternalLoginAsync(mainClient, idpClient, "/t/space-b", "bob");

        SetCookieHeaders(callbackB).Single(h => h.StartsWith($"{ExternalScheme}=", StringComparison.Ordinal))
            .ShouldContain("path=/t/space-b", Case.Insensitive);

        finishB.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK);
        (await finishB.Content.ReadAsStringAsync()).ShouldBe("bob");

        // Both spaces retain their own, independent main sessions.
        var whoAmIA = await mainClient.GetAsync("/t/space-a/test/whoami");
        (await whoAmIA.Content.ReadAsStringAsync()).ShouldBe("alice");

        var whoAmIB = await mainClient.GetAsync("/t/space-b/test/whoami");
        (await whoAmIB.Content.ReadAsStringAsync()).ShouldBe("bob");

        // Neither space's external cookie remains (both were consumed and deleted).
        var externalA = await mainClient.GetAsync($"/t/space-a/test/whoami?scheme={ExternalScheme}");
        externalA.StatusCode.ShouldBe(System.Net.HttpStatusCode.Unauthorized);

        var externalB = await mainClient.GetAsync($"/t/space-b/test/whoami?scheme={ExternalScheme}");
        externalB.StatusCode.ShouldBe(System.Net.HttpStatusCode.Unauthorized);
    }
}
