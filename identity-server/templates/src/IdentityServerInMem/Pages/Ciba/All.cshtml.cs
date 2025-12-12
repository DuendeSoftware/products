using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace IdentityServerHost.Pages.Ciba;

[SecurityHeaders]
[Authorize]
public class AllModel(IBackchannelAuthenticationInteractionService backchannelAuthenticationInteractionService)
    : PageModel
{
    public IEnumerable<BackchannelUserLoginRequest> Logins { get; set; } = default!;

    public async Task OnGet() => Logins = await backchannelAuthenticationInteractionService.GetPendingLoginRequestsForCurrentUserAsync();
}
