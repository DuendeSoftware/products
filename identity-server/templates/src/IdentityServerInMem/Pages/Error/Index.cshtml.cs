using Duende.IdentityServer.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace IdentityServerHost.Pages.Error;

[AllowAnonymous]
[SecurityHeaders]
public class Index(IIdentityServerInteractionService interaction, IWebHostEnvironment environment)
    : PageModel
{
    public ViewModel View { get; set; } = new();

    public async Task OnGet(string? errorId)
    {
        // retrieve error details from IdentityServer
        var message = await interaction.GetErrorContextAsync(errorId);
        if (message != null)
        {
            View.Error = message;

            if (!environment.IsDevelopment())
            {
                // only show in development
                message.ErrorDescription = null;
            }
        }
    }
}
