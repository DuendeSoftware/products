using System.Reflection;
using Duende.IdentityServer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace IdentityServerHost.Pages;

[AllowAnonymous]
public class Index(IdentityServerLicense? license = null) : PageModel
{
    public string Version => typeof(Duende.IdentityServer.Hosting.IdentityServerMiddleware).Assembly
                                 .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                                 ?.InformationalVersion.Split('+').First()
                             ?? "unavailable";
    public IdentityServerLicense? License { get; } = license;
}
