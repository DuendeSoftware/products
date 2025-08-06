using Duende.IdentityServer.EntityFramework.DbContexts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IdentityServerTemplate.Pages.Admin.Components.Clients;

public class ClientsViewComponent(ConfigurationDbContext database) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync()
    {
        var count = await database
            .Clients
            .CountAsync();

        return View(new ClientsViewModel
        {
            Count = count
        });
    }
}

public class ClientsViewModel
{
    public int Count { get; init; }
}
