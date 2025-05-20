using Duende.IdentityServer;
using Duende.IdentityServer.Licensing;
using Microsoft.AspNetCore.Mvc;

namespace IdentityServerQuickStart.ViewComponents;

public class LicenseViewComponent(LicenseUsageSummary summary) : ViewComponent
{
    public Task<IViewComponentResult> InvokeAsync()
    {
        return Task.FromResult<IViewComponentResult>(View(summary));
    }
}
