using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace TemplateWebApp.Pages;

public class LoginModel : PageModel
{
    public IActionResult OnGet(string returnUrl = "/")
    {
        if (!Url.IsLocalUrl(returnUrl))
        {
            returnUrl = "/";
        }

        var props = new AuthenticationProperties
        {
            RedirectUri = returnUrl
        };

        return Challenge(props, OpenIdConnectDefaults.AuthenticationScheme);
    }
}
