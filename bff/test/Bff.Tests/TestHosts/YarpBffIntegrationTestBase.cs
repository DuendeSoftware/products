// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Claims;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Xunit.Abstractions;

namespace Duende.Bff.Tests.TestHosts;

public class YarpBffIntegrationTestBase : OutputWritingTestBase
{
    private readonly IdentityServerHost _identityServerHost;
    protected readonly ApiHost ApiHost;
    protected readonly YarpBffHost YarpBasedBffHost;
    private BffHostUsingResourceNamedTokens _bffHostWithNamedTokens;

    protected YarpBffIntegrationTestBase(ITestOutputHelper output) : base(output)
    {
        _identityServerHost = new IdentityServerHost(WriteLine);

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
                    YarpBasedBffHost!.HttpClient,
                    provider.GetRequiredService<ILoggerFactory>(),
                    provider.GetRequiredService<ICancellationTokenProvider>()));
        };

        ApiHost = new ApiHost(WriteLine, _identityServerHost, "scope1");

        YarpBasedBffHost = new YarpBffHost(output.WriteLine, _identityServerHost, ApiHost, "spa");
        _bffHostWithNamedTokens = new BffHostUsingResourceNamedTokens(WriteLine, _identityServerHost, ApiHost, "spa");
    }

    public async Task Login(string sub)
    {
        await _identityServerHost.IssueSessionCookieAsync(new Claim("sub", sub));
    }

    public override async Task InitializeAsync()
    {
        await _identityServerHost.InitializeAsync();
        await ApiHost.InitializeAsync();
        await YarpBasedBffHost.InitializeAsync();
        await _bffHostWithNamedTokens.InitializeAsync();
        await base.InitializeAsync();
    }

    public override async Task DisposeAsync()
    {
        await _identityServerHost.DisposeAsync();
        await ApiHost.DisposeAsync();
        await YarpBasedBffHost.DisposeAsync();
        await _bffHostWithNamedTokens.DisposeAsync();
        await base.DisposeAsync();
    }
}
