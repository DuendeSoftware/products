using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace IdentityServerTemplate.Pages.Admin.Clients;

[Authorize(Config.Policies.Admin)]
public class DeleteModel(ClientRepository clientRepository) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string ClientId { get; set; } = default!;

    [BindProperty]
    public string? Button { get; set; }

    public void OnGet(string clientId) => ClientId = clientId;

    public async Task<IActionResult> OnPost()
    {
        await clientRepository.DeleteAsync(ClientId);
        return RedirectToPage("/Admin/Clients/Index");
    }
}
