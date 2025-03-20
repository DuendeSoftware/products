namespace IdentityServerHost.Pages.Logout;

public class LoggedOutViewModel
{
    public string? PostLogoutRedirectUri { get; set; }
    public string? ClientName { get; set; }
    public string? SignOutIframeUrl { get; set; }
    public bool AutomaticRedirectAfterSignOut { get; set; }
}
