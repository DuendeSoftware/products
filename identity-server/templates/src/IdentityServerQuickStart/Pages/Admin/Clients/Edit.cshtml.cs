using IdentityServerHost.Pages.Admin.ApiScopes;
using IdentityServerHost.Pages.Admin.IdentityScopes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace IdentityServerHost.Pages.Admin.Clients;

[SecurityHeaders]
[Authorize]
public class EditModel(
    ClientRepository _clientRepository,
    ApiScopeRepository _apiScopeRepository,
    IdentityScopeRepository _identityScopeRepository
    ) : PageModel
{
    private readonly ClientRepository clientRepository = _clientRepository;
    private readonly IdentityScopeRepository identityScopeRepository = _identityScopeRepository;
    private readonly ApiScopeRepository apiScopeRepository = _apiScopeRepository;

    [BindProperty]
    public EditClientModel InputModel { get; set; } = default!;

    public List<ApiScopeSummaryModel> ApiScopes { get; set; } = [];
    public List<IdentityScopeSummaryModel> IdentityScopes { get; set; } = [];

    [BindProperty]
    public string? Button { get; set; }

    public async Task<IActionResult> OnGetAsync(string id)
    {
        var model = await clientRepository.GetByIdAsync(id);
        if (model == null)
        {
            return RedirectToPage("/Admin/Clients/Index");
        }
        else
        {
            ApiScopes = [.. (await apiScopeRepository.GetAllAsync())];
            IdentityScopes = [.. (await identityScopeRepository.GetAllAsync())];
            InputModel = model;
            return Page();
        }
    }

    public async Task<IActionResult> OnPostAsync(string id)
    {
        if (Button == "delete")
        {
            await clientRepository.DeleteAsync(id);
            return RedirectToPage("/Admin/Clients/Index");
        }

        if (ModelState.IsValid)
        {
            await clientRepository.UpdateAsync(InputModel);
            return await OnGetAsync(id);
        }

        return Page();
    }
}
