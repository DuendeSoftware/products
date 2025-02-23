using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Clients;
using System.Net.Http;
using System.Threading.Tasks;

namespace MvcJarUriJwt.Controllers;

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

    public IActionResult Logout() => SignOut("oidc");

    public async Task<IActionResult> CallApi()
    {
        var client = _httpClientFactory.CreateClient("client");

        var response = await client.GetStringAsync("identity");
        ViewBag.Json = response.PrettyPrintJson();

        return View();
    }
}