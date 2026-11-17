// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Claims;
using Duende.IdentityModel;
using Duende.IdentityServer.OutsideIn.Harness.Internal;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.IdentityServer.OutsideIn.Harness;

/// <summary>
/// Typed handle to a real ASP.NET Core client app that signs users in with the OpenID Connect handler (design doc
/// section 05, D8). Its client ID and secret are generated when it's added, so a client can be built from the app with
/// <see cref="ITestClients.CodeFlow" />.
/// </summary>
public interface IWebApp
{
    /// <summary>The name the app was added with. Its URL appears as <c>{name}</c> in snapshots.</summary>
    string Name { get; }

    /// <summary>The app's public URL, for example <c>https://web</c>.</summary>
    Uri BaseUrl { get; }

    Uri RedirectUri { get; }

    Uri PostLogoutRedirectUri { get; }

    /// <summary>
    /// Where a browser goes to sign in: the app challenges an anonymous user, and shows who is signed in afterwards (its
    /// user info endpoint with <see cref="ClientAppBuilder.WithUserInfoEndpoint" />, otherwise a page marked as the app).
    /// </summary>
    Uri SignInUrl { get; }

    /// <summary>The test-only endpoint that returns the signed-in user's claims. Requires <see cref="ClientAppBuilder.WithUserInfoEndpoint" />.</summary>
    Uri UserInfoUrl { get; }

    /// <summary>
    /// A path on this app that is deliberately left unmapped, for a scenario that redeems its own authorization code
    /// (design doc section 09) instead of letting the app's OpenID Connect handler redeem it: the app returns 404 there,
    /// so <see cref="TestBrowser.AuthorizeAsync" /> stops with the code still visible instead of it being redeemed
    /// automatically. A client that uses it must allow it as a redirect URI, for example with
    /// <see cref="ClientBuilderExtensions.WithManualCallback" />.
    /// </summary>
    Uri ManualCallbackUrl { get; }

    /// <summary>Generated, and registered for scrubbing as <c>{client:name}</c>.</summary>
    string ClientId { get; }

    /// <summary>Generated, and registered for scrubbing as <c>{secret:name}</c>.</summary>
    string ClientSecret { get; }
}

/// <summary>Configures a client app added with <see cref="TestEnvironmentBuilder.AddClientApp" />.</summary>
public sealed class ClientAppBuilder
{
    internal ClientAppBuilder()
    {
    }

    internal IIssuer? Authority { get; private set; }

    internal bool HasUserInfoEndpoint { get; private set; }

    /// <summary>The issuer the app signs users in with. Required.</summary>
    public ClientAppBuilder UseAuthority(IIssuer issuer)
    {
        Authority = issuer ?? throw new ArgumentNullException(nameof(issuer));
        return this;
    }

    /// <summary>Adds a test-only endpoint that returns the signed-in user's claims (design doc section 07).</summary>
    public ClientAppBuilder WithUserInfoEndpoint()
    {
        HasUserInfoEndpoint = true;
        return this;
    }
}

/// <summary>One in-process client app on HTTPS, reached through the harness's virtual hosts. One node is enough.</summary>
internal sealed class ClientApp : IWebApp, IAsyncDisposable
{
    private const string UserInfoPath = "/test/userinfo";
    private const string SignInPath = "/test/signin";
    private const string ManualCallbackPath = "/manual-callback";

    private readonly ClientAppBuilder _builder;
    private WebApplication? _app;

    public ClientApp(string name, string hostName, ClientAppBuilder builder)
    {
        Name = name;
        HostName = hostName;
        _builder = builder;
        if (builder.Authority is null)
        {
            throw new InvalidOperationException($"Client app '{name}' needs an authority. Call UseAuthority(issuer) when adding it.");
        }
    }

    public string Name { get; }

    public string HostName { get; }

    public Uri BaseUrl => new($"https://{HostName}");

    public Uri RedirectUri => new(BaseUrl, "/signin-oidc");

    public Uri PostLogoutRedirectUri => new(BaseUrl, "/signout-callback-oidc");

    public Uri SignInUrl => new(BaseUrl, SignInPath);

    public Uri UserInfoUrl => _builder.HasUserInfoEndpoint
        ? new Uri(BaseUrl, UserInfoPath)
        : throw new InvalidOperationException($"Client app '{Name}' has no user info endpoint. Add it with WithUserInfoEndpoint().");

    public Uri ManualCallbackUrl => new(BaseUrl, ManualCallbackPath);

    public string ClientId { get; } = Guid.NewGuid().ToString("N");

    public string ClientSecret { get; } = Guid.NewGuid().ToString("N");

    public async Task StartAsync(TestClock clock, VirtualHostRegistry virtualHosts, HarnessLog log, Ct cancellationToken)
    {
        var builder = HarnessHosting.CreateBuilder(clock);
        builder.ListenOnLoopback(https: true);
        new SharedDataProtection($"client-app:{HostName}", clock).Configure(builder.Services);

        builder.Services
            .AddAuthentication(options =>
            {
                options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
            })
            .AddCookie()
            .AddOpenIdConnect(options =>
            {
                options.Authority = _builder.Authority!.Url.ToString();
                options.ClientId = ClientId;
                options.ClientSecret = ClientSecret;
                options.ResponseType = OidcConstants.ResponseTypes.Code;
                options.UsePkce = true;
                options.Scope.Clear();
                foreach (var scope in TestDefaults.ClientScopes)
                {
                    options.Scope.Add(scope);
                }

                options.GetClaimsFromUserInfoEndpoint = true;
                options.MapInboundClaims = false;
                options.SaveTokens = false;
                options.TokenValidationParameters.NameClaimType = JwtClaimTypes.Name;
                HarnessOidcClient.Apply(options, Name, clock, virtualHosts);
            });
        builder.Services.AddAuthorization();

        var app = builder.Build();
        app.UseRequestNumbering(log);
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapGet(SignInPath, () => _builder.HasUserInfoEndpoint
                ? Results.Redirect(UserInfoPath)
                : Results.Content("<!DOCTYPE html><html><body data-page=\"app\"></body></html>", "text/html"))
            .RequireAuthorization();
        if (_builder.HasUserInfoEndpoint)
        {
            app.MapGet(UserInfoPath, (ClaimsPrincipal user, HttpResponse response) =>
            {
                // Not HTML, so the page marker travels in a header.
                response.Headers[TestBrowser.PageHeader] = AppPage.Marker;
                return Results.Json(user.Claims
                    .GroupBy(claim => claim.Type)
                    .ToDictionary(group => group.Key, group => group.Count() == 1 ? (object)group.First().Value : group.Select(claim => claim.Value).ToArray()));
            })
                .RequireAuthorization();
        }

        await app.StartAsync(cancellationToken);
        _app = app;
        virtualHosts.Register(HostName, app.GetPort(), $"client app {Name}");
    }

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }
}
