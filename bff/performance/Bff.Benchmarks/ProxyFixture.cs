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
    public PlainYarpProxy YarpProxy;

    public ProxyFixture()
    {
        IdentityServer = new IdentityServerHost();
        IdentityServer.Initialize();


        Api = new ApiHost(IdentityServer.Url);
        Api.Initialize();

        Bff = new BffHost(IdentityServer.Url, Api.Url);
        Bff.Initialize();

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

            RedirectUris = { $"{Bff.Url}signin-oidc" },
            FrontChannelLogoutUri = $"{Bff.Url}signout-oidc",
            PostLogoutRedirectUris = { $"{Bff.Url}signout-callback-oidc" },

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
