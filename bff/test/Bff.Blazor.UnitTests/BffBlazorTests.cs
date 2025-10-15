// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using Duende.Bff;
using Duende.Bff.Tests.TestHosts;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;

namespace Bff.Blazor.UnitTests;

public class BffBlazorTests : OutputWritingTestBase
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;
    private readonly IdentityServerHost _identityServerHost;
    private readonly ApiHost _apiHost;
    private readonly BffBlazorHost _bffHost;

    public BffBlazorTests(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
    {
        _identityServerHost = new IdentityServerHost(WriteLine);
        _apiHost = new ApiHost(WriteLine, _identityServerHost, "scope1");

        _bffHost = new BffBlazorHost(WriteLine, _identityServerHost, _apiHost, "spa");

        _identityServerHost.Clients.Add(new Client
        {
            ClientId = "spa",
            ClientSecrets = { new Secret("secret".Sha256()) },
            AllowedGrantTypes = GrantTypes.CodeAndClientCredentials,
            RedirectUris = { "https://app/signin-oidc" },
            PostLogoutRedirectUris = { "https://app/signout-callback-oidc" },
            BackChannelLogoutUri = "https://app/bff/backchannel",
            AllowOfflineAccess = true,
            AllowedScopes = { "openid", "profile", "scope1" }
        });



        _identityServerHost.OnConfigureServices += services =>
        {
            services.AddTransient<IBackChannelLogoutHttpClient>(provider =>
                new DefaultBackChannelLogoutHttpClient(
                    _bffHost!.HttpClient,
                    provider.GetRequiredService<ILoggerFactory>(),
                    provider.GetRequiredService<ICancellationTokenProvider>()));

            services.AddSingleton<DefaultAccessTokenRetriever>();
        };
    }

    [Fact]
    public async Task Can_get_home()
    {
        var response = await _bffHost.BrowserClient.GetAsync("/", _ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Cannot_get_secure_without_logging_in()
    {
        var response = await _bffHost.BrowserClient.GetAsync("/secure", _ct);
        response.StatusCode.ShouldBe(HttpStatusCode.Found, "this indicates we are redirecting to the login page");
    }

    [Fact]
    public async Task Can_get_secure_when_logged_in()
    {
        await _bffHost.BffLoginAsync("sub");
        var response = await _bffHost.BrowserClient.GetAsync("/secure", _ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    public override async ValueTask InitializeAsync()
    {
        await _identityServerHost.InitializeAsync();
        await _apiHost.InitializeAsync();
        await _bffHost.InitializeAsync();
        await base.InitializeAsync();
    }

    public override async ValueTask DisposeAsync()
    {
        await _apiHost.DisposeAsync();
        await _bffHost.DisposeAsync();
        await _identityServerHost.DisposeAsync();
        await base.DisposeAsync();
    }
}
