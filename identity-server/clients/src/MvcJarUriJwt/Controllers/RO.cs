using System.Net.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MvcJarUriJwt.Controllers;

[AllowAnonymous]
public class ROController : Controller
{
    private readonly RequestUriService _requestUriService;

    public ROController(RequestUriService requestUriService)
    {
        _requestUriService = requestUriService;
    }
    
    public IActionResult Index(string id)
    {
        var value = _requestUriService.Get(id);
        if (!string.IsNullOrWhiteSpace(value))
        {
            return Content(value);
        }

        return NotFound();
    }
}