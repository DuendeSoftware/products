using Duende.IdentityServer.EntityFramework.DbContexts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IdentityServerQuickStart.Pages.Admin.Components.ApiScopes;

public class ApiScopesViewComponent(ConfigurationDbContext database) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync()
    {
        var count = await database.ApiScopes.CountAsync();

        return View(new ApiScopesViewModel
        {
            Count = count
        });
    }
}

public class ApiScopesViewModel
{
    public int Count { get; init; }
}
