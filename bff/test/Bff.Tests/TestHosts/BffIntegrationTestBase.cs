// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Claims;
using Duende.Bff.Internal;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Xunit.Abstractions;

namespace Duende.Bff.Tests.TestHosts;

public class BffIntegrationTestBase : OutputWritingTestBase
{
    protected readonly IdentityServerHost IdentityServer;
    protected ApiHost Api;
    protected BffHost Bff;
    protected BffHostUsingResourceNamedTokens BffHostWithNamedTokens;

    public BffIntegrationTestBase(ITestOutputHelper output) : base(output)
    {
        IdentityServer = new IdentityServerHost(WriteLine);
        Api = new ApiHost(WriteLine, IdentityServer, "scope1");
        Bff = new BffHost(WriteLine, IdentityServer, Api, "spa");
        BffHostWithNamedTokens = new BffHostUsingResourceNamedTokens(WriteLine, IdentityServer, Api, "spa");

        IdentityServer.Clients.Add(new Client
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


        IdentityServer.OnConfigureServices += services =>
        {
            services.AddTransient<IBackChannelLogoutHttpClient>(provider =>
                new DefaultBackChannelLogoutHttpClient(
                    Bff!.HttpClient,
                    provider.GetRequiredService<ILoggerFactory>(),
                    provider.GetRequiredService<ICancellationTokenProvider>()));

            services.AddSingleton<DefaultAccessTokenRetriever>();
        };
    }

    public async Task Login(string sub) => await IdentityServer.IssueSessionCookieAsync(new Claim("sub", sub));

    public override async Task InitializeAsync()
    {
        await IdentityServer.InitializeAsync();
        await Api.InitializeAsync();
        await Bff.InitializeAsync();
        await BffHostWithNamedTokens.InitializeAsync();
        await base.InitializeAsync();
    }

    public override async Task DisposeAsync()
    {
        await Api.DisposeAsync();
        await Bff.DisposeAsync();
        await BffHostWithNamedTokens.DisposeAsync();
        await IdentityServer.DisposeAsync();
        await base.DisposeAsync();
    }
}
