using System.Diagnostics.CodeAnalysis;
using Duende.IdentityServer;
using Microsoft.AspNetCore.Mvc;

namespace IdentityServerTemplate.ViewComponents;

public class LicenseViewComponent(IdentityServerLicense? license = null) : ViewComponent
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
    public IdentityServerLicense? License { get; init; }
    [MemberNotNullWhen(true, nameof(License))]
    public bool HasLicense => License is not null;
    public string LicenseText => License?.Edition.ToString() ?? "Trial";
}
