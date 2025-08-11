using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace TemplateWebApp.Pages;

public class LogoutModel : PageModel
{
    public IActionResult OnGet()
    {
        var props = new AuthenticationProperties
        {
            RedirectUri = Url.Content("~/")
        };

        return SignOut(props,
            OpenIdConnectDefaults.AuthenticationScheme,
            CookieAuthenticationDefaults.AuthenticationScheme);

    }
}
