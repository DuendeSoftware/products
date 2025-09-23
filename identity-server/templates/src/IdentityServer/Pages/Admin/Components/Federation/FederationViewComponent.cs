using Duende.IdentityServer.Services;
using Duende.IdentityServer.Stores;
using IdentityServerTemplate.Pages.Admin.Federation;
using Microsoft.AspNetCore.Mvc;

namespace IdentityServerTemplate.Pages.Admin.Components.Federation;

public class FederationViewComponent(FederationRepository repository) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync()
    {
        var result = (await repository.GetAllAsync()).Count();

        var vm = new FederationViewModel
        {
            Count = result
        };
        return View(vm);
    }
}

public class FederationViewModel
{
    public int Count { get; init; }
}
