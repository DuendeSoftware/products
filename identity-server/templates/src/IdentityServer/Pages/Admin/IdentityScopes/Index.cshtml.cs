using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace IdentityServerTemplate.Pages.Admin.IdentityScopes;

[SecurityHeaders]
[Authorize(Config.Policies.Admin)]
public class IndexModel(IdentityScopeRepository repository) : PageModel
{
    public IEnumerable<IdentityScopeSummaryModel> Scopes { get; private set; } = default!;
    public string? Filter { get; set; }

    public async Task OnGetAsync(string? filter)
    {
        Filter = filter;
        Scopes = await repository.GetAllAsync(filter);
    }
}
