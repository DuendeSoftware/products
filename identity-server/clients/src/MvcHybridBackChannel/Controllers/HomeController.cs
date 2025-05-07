// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Globalization;
using Clients;
using Duende.IdentityModel.Client;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MvcHybrid.Controllers;

public class HomeController : Controller
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IDiscoveryCache _discoveryCache;
    private readonly IConfiguration _configuration;

    public HomeController(IHttpClientFactory httpClientFactory, IDiscoveryCache discoveryCache, IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _discoveryCache = discoveryCache;
        _configuration = configuration;
    }

    public IActionResult Index() => View();

    [Authorize]
    public IActionResult Secure() => View();

    [Authorize]
    public async Task<IActionResult> CallApi()
    {
        // Resolve the HttpClient from DI.
        var client = _httpClientFactory.CreateClient("SimpleApi");
        var token = await HttpContext.GetTokenAsync("access_token");

        client.SetBearerToken(token);

        var response = await client.GetStringAsync("/identity");
        ViewBag.Json = response.PrettyPrintJson();

        return View();
    }

    public async Task<IActionResult> RenewTokens()
    {
        var disco = await _discoveryCache.GetAsync();
        if (disco.IsError)
        {
            throw new Exception(disco.Error);
        }

        var rt = await HttpContext.GetTokenAsync("refresh_token");
        var tokenClient = _httpClientFactory.CreateClient();

        var tokenResult = await tokenClient.RequestRefreshTokenAsync(new RefreshTokenRequest
        {
            Address = disco.TokenEndpoint,

            ClientId = "mvc.hybrid.backchannel",
            ClientSecret = "secret",
            RefreshToken = rt
        });

        if (!tokenResult.IsError)
        {
            var old_id_token = await HttpContext.GetTokenAsync("id_token");
            var new_access_token = tokenResult.AccessToken;
            var new_refresh_token = tokenResult.RefreshToken;
            var expiresAt = DateTime.UtcNow + TimeSpan.FromSeconds(tokenResult.ExpiresIn);

            var info = await HttpContext.AuthenticateAsync("Cookies");

            info.Properties.UpdateTokenValue("refresh_token", new_refresh_token);
            info.Properties.UpdateTokenValue("access_token", new_access_token);
            info.Properties.UpdateTokenValue("expires_at", expiresAt.ToString("o", CultureInfo.InvariantCulture));

            await HttpContext.SignInAsync("Cookies", info.Principal, info.Properties);
            return Redirect("~/Home/Secure");
        }

        ViewData["Error"] = tokenResult.Error;
        return View("Error");
    }

    public IActionResult Logout() => new SignOutResult(new[] { "Cookies", "oidc" });

    public IActionResult Error() => View();
}
