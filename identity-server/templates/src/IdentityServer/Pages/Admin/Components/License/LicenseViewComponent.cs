using System.Diagnostics.CodeAnalysis;
using Duende.IdentityServer.Licensing;
using Microsoft.AspNetCore.Mvc;

namespace IdentityServerTemplate.ViewComponents;

public class LicenseViewComponent(LicenseInformation? license = null) : ViewComponent
{
    public Task<IViewComponentResult> InvokeAsync()
    {
        var vm = new LicenseViewModel
        {
            License = license
        };

        return Task.FromResult<IViewComponentResult>(View(vm));
    }
}

public class LicenseViewModel
{
    public LicenseInformation? License { get; init; }
    [MemberNotNullWhen(true, nameof(License))]
    public bool HasLicense => License is not null && License.IsConfigured;
    public string LicenseText => License?.IsConfigured == true ? "Licensed" : "Trial";
}
