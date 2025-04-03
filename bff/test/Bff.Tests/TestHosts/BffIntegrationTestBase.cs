// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Claims;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Xunit.Abstractions;

namespace Duende.Bff.Tests.TestHosts;

public class BffIntegrationTestBase : OutputWritingTestBase
{
    protected readonly IdentityServerHost IdentityServerHost;
    protected ApiHost ApiHost;
    protected BffHost BffHost;
    protected BffHostUsingResourceNamedTokens BffHostWithNamedTokens;

    public BffIntegrationTestBase(ITestOutputHelper output) : base(output)
    {
        IdentityServerHost = new IdentityServerHost(WriteLine);
        ApiHost = new ApiHost(WriteLine, IdentityServerHost, "scope1");
        BffHost = new BffHost(WriteLine, IdentityServerHost, ApiHost, "spa");
        BffHostWithNamedTokens = new BffHostUsingResourceNamedTokens(WriteLine, IdentityServerHost, ApiHost, "spa");

        IdentityServerHost.Clients.Add(new Client
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


        IdentityServerHost.OnConfigureServices += services =>
        {
            services.AddTransient<IBackChannelLogoutHttpClient>(provider =>
                new DefaultBackChannelLogoutHttpClient(
                    BffHost!.HttpClient,
                    provider.GetRequiredService<ILoggerFactory>(),
                    provider.GetRequiredService<ICancellationTokenProvider>()));

#pragma warning disable CS0618 // Type or member is obsolete
            services.AddSingleton<DefaultAccessTokenRetriever>();
#pragma warning restore CS0618 // Type or member is obsolete
        };
    }

    public async Task Login(string sub)
    {
        await IdentityServerHost.IssueSessionCookieAsync(new Claim("sub", sub));
    }

    public override async Task InitializeAsync()
    {
        await IdentityServerHost.InitializeAsync();
        await ApiHost.InitializeAsync();
        await BffHost.InitializeAsync();
        await BffHostWithNamedTokens.InitializeAsync();
        await base.InitializeAsync();
    }

    public override async Task DisposeAsync()
    {
        await ApiHost.DisposeAsync();
        await BffHost.DisposeAsync();
        await BffHostWithNamedTokens.DisposeAsync();
        await IdentityServerHost.DisposeAsync();
        await base.DisposeAsync();
    }
}
