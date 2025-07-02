using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace IdentityServerQuickStart.Pages.Admin.ApiScopes;

[SecurityHeaders]
[Authorize(Config.Policies.Admin)]
public class IndexModel(ApiScopeRepository repository) : PageModel
{
    public IEnumerable<ApiScopeSummaryModel> Scopes { get; private set; } = default!;
    public string? Filter { get; set; }

    public async Task OnGetAsync(string? filter)
    {
        Filter = filter;
        Scopes = await repository.GetAllAsync(filter);
    }
}
