// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Globalization;
using Duende.AccessTokenManagement;
using Duende.AccessTokenManagement.OpenIdConnect;
using Duende.IdentityModel.Client;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace Duende.IdentityServer.UI.ClientWebApp.Pages;

public class RenewTokensModel : PageModel
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IDiscoveryCache _discoveryCache;
    private readonly OpenIdConnectOptions _oidcOptions;
    private readonly IUserTokenManager? _tokenManager;

    public RenewTokensModel(
        IHttpClientFactory httpClientFactory,
        IDiscoveryCache discoveryCache,
        IOptionsMonitor<OpenIdConnectOptions> oidcOptionsMonitor,
        IUserTokenManager? tokenManager = null)
    {
        _httpClientFactory = httpClientFactory;
        _discoveryCache = discoveryCache;
        _oidcOptions = oidcOptionsMonitor.Get("oidc");
        _tokenManager = tokenManager;
    }

    public string? Error { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        // When AccessTokenManagement is registered, use it for token renewal.
        // This handles DPoP proofs, client assertions, and token storage automatically.
        if (_tokenManager != null)
        {
            await HttpContext.GetUserAccessTokenAsync(
                new UserTokenRequestParameters { ForceTokenRenewal = true })
                .GetToken();

            return RedirectToPage("Secure");
        }

        // Fallback: manual token renewal for scenarios without AccessTokenManagement
        var disco = await _discoveryCache.GetAsync();
        if (disco.IsError)
        {
            throw new Exception(disco.Error);
        }

        var rt = await HttpContext.GetTokenAsync("refresh_token");
        var tokenClient = _httpClientFactory.CreateClient();

        var request = new RefreshTokenRequest
        {
            Address = disco.TokenEndpoint,
            ClientId = _oidcOptions.ClientId!,
            ClientSecret = _oidcOptions.ClientSecret!,
            RefreshToken = rt!
        };

        var tokenResult = await tokenClient.RequestRefreshTokenAsync(request);

        if (!tokenResult.IsError)
        {
            var newAccessToken = tokenResult.AccessToken;
            var newRefreshToken = tokenResult.RefreshToken;
            var expiresAt = DateTime.UtcNow + TimeSpan.FromSeconds(tokenResult.ExpiresIn);

            var info = await HttpContext.AuthenticateAsync("Cookies");

            info.Properties!.UpdateTokenValue("refresh_token", newRefreshToken);
            info.Properties!.UpdateTokenValue("access_token", newAccessToken);
            info.Properties!.UpdateTokenValue("expires_at", expiresAt.ToString("o", CultureInfo.InvariantCulture));

            await HttpContext.SignInAsync("Cookies", info.Principal!, info.Properties);
            return RedirectToPage("Secure");
        }

        Error = tokenResult.Error;
        return Page();
    }
}
