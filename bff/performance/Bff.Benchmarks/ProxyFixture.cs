// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Bff.Benchmarks.Hosts;
using Duende.IdentityModel;
using Duende.IdentityServer.Models;

namespace Bff.Benchmarks;

public class ProxyFixture : IAsyncDisposable
{
    public ApiHost Api;
    public IdentityServerHost IdentityServer;
    public BffHost Bff;
    public BffHost BffWithManyFrontends;
    public PlainYarpProxy YarpProxy;

    public ProxyFixture()
    {
        IdentityServer = new IdentityServerHost();
        IdentityServer.Initialize();


        Api = new ApiHost(IdentityServer.Url);
        Api.Initialize();

        Bff = new BffHost(IdentityServer.Url, Api.Url);
        Bff.Initialize();
        BffWithManyFrontends = new BffHost(IdentityServer.Url, Api.Url);
        BffWithManyFrontends.Initialize();

        for (var i = 0; i < 1000; i++)
        {
            BffWithManyFrontends.AddFrontend(new Uri($"https://frontend{i}.example.com/"));
        }
        BffWithManyFrontends.AddFrontend(Bff.Url);

        var bffUrls = new[] { Bff.Url, BffWithManyFrontends.Url };
        IdentityServer.Clients.Add(new Client()
        {
            ClientId = "bff",
            ClientSecrets = { new Secret("secret".Sha256()) },

            AllowedGrantTypes =
            {
                GrantType.AuthorizationCode,
                GrantType.ClientCredentials,
                OidcConstants.GrantTypes.TokenExchange
            },

            RedirectUris = bffUrls.Select(x => $"{x}signin-oidc").ToArray(),
            PostLogoutRedirectUris = bffUrls.Select(x => $"{x}signout-callback-oidc").ToArray(),

            AllowOfflineAccess = true,
            AllowedScopes = { "openid", "profile", "api" },

            RefreshTokenExpiration = TokenExpiration.Absolute,
            AbsoluteRefreshTokenLifetime = 300,
            AccessTokenLifetime = 3000
        });

        YarpProxy = new PlainYarpProxy(Api.Url);
        YarpProxy.Initialize();
    }



    public async ValueTask DisposeAsync()
    {
        await IdentityServer.DisposeAsync();
        await Api.DisposeAsync();
        await Bff.DisposeAsync();
        await YarpProxy.DisposeAsync();
    }
}
