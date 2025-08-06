using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace IdentityServerTemplate.Pages.Admin.Clients;

[SecurityHeaders]
[Authorize(Config.Policies.Admin)]
public class IndexModel(ClientRepository repository) : PageModel
{
    public IEnumerable<ClientSummaryModel> Clients { get; private set; } = default!;
    public string? Filter { get; set; }

    public async Task OnGetAsync(string? filter)
    {
        Filter = filter;
        Clients = await repository.GetAllAsync(filter);
    }
}
