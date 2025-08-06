using Duende.IdentityServer.Services;
using Duende.IdentityServer.Stores;
using Microsoft.AspNetCore.Mvc;

namespace IdentityServerQuickStart.Pages.Admin.Components.ServerSideSessions;

public class ServerSideSessionsViewComponent(ISessionManagementService sessions) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync()
    {
        var result = await
            sessions.QuerySessionsAsync(new SessionQuery());

        var vm = new ServerSideSessionsViewModel
        {
            Count = result.TotalCount.GetValueOrDefault()
        };
        return View(vm);
    }
}

public class ServerSideSessionsViewModel
{
    public int Count { get; init; }
}
