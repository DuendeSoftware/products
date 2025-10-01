using Duende;
using Duende.IdentityServer;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Stores;
using IdentityServerTemplate.Pages.Admin.Federation;
using Microsoft.AspNetCore.Mvc;

namespace IdentityServerTemplate.Pages.Admin.Components.Federation;

public class FederationViewComponent(FederationRepository repository, IdentityServerLicense? license = null) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync()
    {
        var result = (await repository.GetAllAsync()).Count();

        var vm = new FederationViewModel
        {
            Count = result,
            IsLicensed = license != null && license.Edition == License.LicenseEdition.Enterprise
        };
        return View(vm);
    }
}

public class FederationViewModel
{
    public int Count { get; init; }
    public bool IsLicensed { get; init; }
}
