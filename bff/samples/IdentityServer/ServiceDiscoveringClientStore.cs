// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityModel;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;
using Hosts.ServiceDefaults;
using Microsoft.Extensions.ServiceDiscovery;

namespace IdentityServer;

/// <summary>
/// This client store will register a list of hard coded clients but will use
/// service discovery to ask for the correct urls.
///
/// This is needed because the actually used url's need to be set in Identity Server. 
/// </summary>
/// <param name="resolver"></param>
public class ServiceDiscoveringClientStore(ServiceEndpointResolver resolver) : IClientStore
{
    private List<Client> _clients = null;
    private SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
    private async Task Initialize()
    {

        await _semaphore.WaitAsync();
        try
        {


            if (_clients != null)
            {
                return;
            }
            // Get the BFF URL from the service discovery system. Then use this for building the redirect urls etc..
            var bffUrl = await GetUrlAsync(AppHostServices.Bff);
            var bffDPopUrl = await GetUrlAsync(AppHostServices.BffDpop);
            var bffEfUrl = await GetUrlAsync(AppHostServices.BffEf);
            var bffBlazorPerComponentUrl = await GetUrlAsync(AppHostServices.BffBlazorPerComponent);
            var bffBlazorWebAssemblyUrl = await GetUrlAsync(AppHostServices.BffBlazorWebassembly);

            _clients = [
                new Client
                {
                    ClientId = "bff",
                    ClientSecrets = { new Secret("secret".Sha256()) },

                    AllowedGrantTypes =
                    {
                        GrantType.AuthorizationCode,
                        GrantType.ClientCredentials,
                        OidcConstants.GrantTypes.TokenExchange
                    },

                    RedirectUris = { $"{bffUrl}signin-oidc" },
                    FrontChannelLogoutUri = $"{bffUrl}signout-oidc",
                    PostLogoutRedirectUris = { $"{bffUrl}signout-callback-oidc" },

                    AllowOfflineAccess = true,
                    AllowedScopes = { "openid", "profile", "api", "scope-for-isolated-api" },

                    AccessTokenLifetime = 75 // Force refresh
                },
                new Client
                {
                    ClientId = "bff.dpop",
                    ClientSecrets = { new Secret("secret".Sha256()) },
                    RequireDPoP = true,

                    AllowedGrantTypes =
                    {
                        GrantType.AuthorizationCode,
                        GrantType.ClientCredentials,
                        OidcConstants.GrantTypes.TokenExchange
                    },

                    RedirectUris = { $"{bffDPopUrl}signin-oidc" },
                    FrontChannelLogoutUri = $"{bffDPopUrl}signout-oidc",
                    PostLogoutRedirectUris = { $"{bffDPopUrl}signout-callback-oidc" },

                    AllowOfflineAccess = true,
                    AllowedScopes = { "openid", "profile", "api", "scope-for-isolated-api" },

                    AccessTokenLifetime = 75 // Force refresh
                },
                new Client
                {
                    ClientId = "bff.ef",
                    ClientSecrets = { new Secret("secret".Sha256()) },

                    AllowedGrantTypes =
                    {
                        GrantType.AuthorizationCode,
                        GrantType.ClientCredentials,
                        OidcConstants.GrantTypes.TokenExchange
                    },
                    RedirectUris = { $"{bffEfUrl}signin-oidc" },
                    FrontChannelLogoutUri = $"{bffEfUrl}signout-oidc",
                    BackChannelLogoutUri = $"{bffEfUrl}bff/backchannel",
                    PostLogoutRedirectUris = { $"{bffEfUrl}signout-callback-oidc" },

                    AllowOfflineAccess = true,
                    AllowedScopes = { "openid", "profile", "api", "scope-for-isolated-api" },

                    AccessTokenLifetime = 75 // Force refresh
                },

                new Client
                {
                    ClientId = "blazor",
                    ClientSecrets = { new Secret("secret".Sha256()) },

                    AllowedGrantTypes =
                    {
                        GrantType.AuthorizationCode,
                        GrantType.ClientCredentials,
                        OidcConstants.GrantTypes.TokenExchange
                    },

                    RedirectUris = { $"{bffBlazorWebAssemblyUrl}signin-oidc", $"{bffBlazorPerComponentUrl}signin-oidc" },
                    PostLogoutRedirectUris =
                    {
                        $"{bffBlazorWebAssemblyUrl}signout-callback-oidc", $"{bffBlazorPerComponentUrl}signout-callback-oidc"
                    },

                    AllowOfflineAccess = true,
                    AllowedScopes = { "openid", "profile", "api", "scope-for-isolated-api" },

                    AccessTokenLifetime = 75
                }
            ];
        }
        finally
        {
            _semaphore.Release();
        }

    }

    private async Task<string> GetUrlAsync(string serviceName)
    {
        return (await resolver.GetEndpointsAsync("https://" + serviceName, CancellationToken.None)).Endpoints.First().EndPoint.ToString();
    }


    public async Task<Client> FindClientByIdAsync(string clientId)
    {
        await Initialize();
        return _clients?.FirstOrDefault(x => x.ClientId == clientId);
    }
}