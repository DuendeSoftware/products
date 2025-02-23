using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Net.Http;
using System.Threading.Tasks;
using Clients;
using Microsoft.AspNetCore.Authentication;
using Duende.AccessTokenManagement.OpenIdConnect;

namespace MvcDPoP.Controllers;

public class HomeController : Controller
{
    private readonly IHttpClientFactory _httpClientFactory;

    public HomeController(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    [AllowAnonymous]
    public IActionResult Index() => View();

    public IActionResult Secure() => View();
    
    public async Task<IActionResult> Renew()
    {
        await HttpContext.GetUserAccessTokenAsync(new UserTokenRequestParameters { ForceRenewal = true });
        return RedirectToAction(nameof(Secure));
    }

    public IActionResult Logout() => SignOut("oidc");

    public async Task<IActionResult> CallApi()
    {
        var client = _httpClientFactory.CreateClient("client");

        var response = await client.GetStringAsync("identity");
        ViewBag.Json = response.PrettyPrintJson();
        
        return View();
    }


}