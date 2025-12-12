using Duende.IdentityServer.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace IdentityServerHost.Pages.Account.Logout;

[SecurityHeaders]
[AllowAnonymous]
public class LoggedOut(IIdentityServerInteractionService interactionService) : PageModel
{
    public LoggedOutViewModel View { get; set; } = default!;

    public async Task OnGet(string? logoutId)
    {
        // get context information (client name, post logout redirect URI and iframe for federated signout)
        var logout = await interactionService.GetLogoutContextAsync(logoutId);

        View = new LoggedOutViewModel
        {
            AutomaticRedirectAfterSignOut = LogoutOptions.AutomaticRedirectAfterSignOut,
            PostLogoutRedirectUri = logout?.PostLogoutRedirectUri,
            ClientName = string.IsNullOrEmpty(logout?.ClientName) ? logout?.ClientId : logout?.ClientName,
            SignOutIframeUrl = logout?.SignOutIFrameUrl
        };
    }
}
