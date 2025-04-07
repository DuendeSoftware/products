using System.Reflection;
using Duende.IdentityServer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace IdentityServerHost.Pages.Home;

[AllowAnonymous]
public class Index : PageModel
{
    public Index(IdentityServerLicense? license = null) => License = license;

    public string Version => typeof(Duende.IdentityServer.Hosting.IdentityServerMiddleware).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion.Split('+').First()
            ?? "unavailable";
    public IdentityServerLicense? License { get; }
}
