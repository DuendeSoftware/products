using Duende.IdentityServer.EntityFramework.DbContexts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IdentityServerTemplate.Pages.Admin.Components.IdentityScopes;

public class IdentityScopesViewComponent(ConfigurationDbContext database) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync()
    {
        var count = await database.IdentityResources.CountAsync();
        return View(new IdentityScopesViewModel
        {
            Count = count
        });
    }
}

public class IdentityScopesViewModel
{
    public int Count { get; init; }
}
