using Duende.IdentityServer.Events;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Stores;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace IdentityServerHost.Pages.Grants;

[SecurityHeaders]
[Authorize]
public class Index(
    IIdentityServerInteractionService interaction,
    IClientStore clients,
    IResourceStore resources,
    IEventService events)
    : PageModel
{
    public ViewModel View { get; set; } = default!;

    public async Task OnGetAsync(CancellationToken ct)
    {
        var grants = await interaction.GetAllUserGrantsAsync(ct);

        var list = new List<GrantViewModel>();
        foreach (var grant in grants)
        {
            var client = await clients.FindClientByIdAsync(grant.ClientId, ct);
            if (client != null)
            {
                var resources1 = await resources.FindResourcesByScopeAsync(grant.Scopes, ct);

                var item = new GrantViewModel()
                {
                    ClientId = client.ClientId,
                    ClientName = client.ClientName ?? client.ClientId,
                    ClientLogoUrl = client.LogoUri,
                    ClientUrl = client.ClientUri,
                    Description = grant.Description,
                    Created = grant.CreationTime,
                    Expires = grant.Expiration,
                    IdentityGrantNames = resources1.IdentityResources.Select(x => x.DisplayName ?? x.Name).ToArray(),
                    ApiGrantNames = resources1.ApiScopes.Select(x => x.DisplayName ?? x.Name).ToArray()
                };

                list.Add(item);
            }
        }

        View = new ViewModel
        {
            Grants = list
        };
    }

    [BindProperty]
    public string? ClientId { get; set; }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        await interaction.RevokeUserConsentAsync(ClientId, ct);
        await events.RaiseAsync(new GrantsRevokedEvent(User.GetSubjectId(), ClientId), ct);
        Telemetry.Metrics.GrantsRevoked(ClientId);

        return RedirectToPage("/Grants/Index");
    }
}
