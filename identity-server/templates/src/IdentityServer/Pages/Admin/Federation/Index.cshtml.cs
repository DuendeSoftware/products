using Duende;
using Duende.IdentityServer;
using IdentityServerTemplate.Pages.Admin.Federation.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace IdentityServerTemplate.Pages.Admin.Federation;

[SecurityHeaders]
[Authorize(Config.Policies.Admin)]
public class IndexModel(FederationRepository repository, IdentityServerLicense? license = null) : PageModel
{
    public IEnumerable<ProviderConfigurationInfo> ProviderConfigurationInfo { get; private set; } = default!;
    public IEnumerable<ProviderSummaryModel> Providers { get; private set; } = default!;
    public string? Filter { get; set; }
    public bool IsLicensed = license != null && license.Edition == License.LicenseEdition.Enterprise;

    public async Task OnGetAsync(string? filter)
    {
        Filter = filter;
        ProviderConfigurationInfo = repository.GetAllProviderConfigurationInfo();
        Providers = await repository.GetAllAsync(filter);
    }
}
