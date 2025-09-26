using Duende.IdentityServer.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace IdentityServerTemplate.Pages.Admin.Federation;

[SecurityHeaders]
[Authorize(Config.Policies.Admin)]
public class EditModel(
    FederationRepository repository,
    IdentityServerOptions identityServerOptions,
    IHttpContextAccessor httpContextAccessor
    ) : PageModel
{
    [BindProperty]
    public EditProviderModel InputModel { get; set; } = default!;

    public bool Updated { get; set; }

    [BindProperty]
    public string? Button { get; set; }

    public async Task<IActionResult> OnGetAsync(string scheme)
    {
        var model = await repository.GetBySchemeAsync(scheme);
        if (model == null)
        {
            return RedirectToPage("/Admin/Federation/Index");
        }
        else
        {
            InputModel = model;

            var callbackUrlBuilder = new UriBuilder(
                httpContextAccessor.HttpContext?.Request.Scheme + "://" + httpContextAccessor.HttpContext?.Request.Host + "/");
            callbackUrlBuilder.Path = identityServerOptions.DynamicProviders.PathPrefix + "/" + InputModel.Scheme + "/signin-oidc";
            InputModel.CallbackUrl = callbackUrlBuilder.ToString();

            return Page();
        }
    }

    public async Task<IActionResult> OnPostAsync(string scheme)
    {
        if (Button == "delete")
        {
            await repository.DeleteAsync(scheme);
            return RedirectToPage("/Admin/Federation/Index");
        }

        if (ModelState.IsValid)
        {
            await repository.UpdateAsync(InputModel);
            Updated = true;

            return RedirectToPage("/Admin/Federation/Edit", new { scheme });
        }

        return Page();
    }
}
