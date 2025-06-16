using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Stores;
using IdentityServerQuickStart.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace IdentityServerQuickStart.Pages.ServerSideSessions;

[Authorize(Config.Policies.Admin)]
public class IndexModel(ISessionManagementService? sessionManagementService = null) : PageModel
{
    public QueryResult<UserSession>? UserSessions { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? DisplayNameFilter { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? SessionIdFilter { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? SubjectIdFilter { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Token { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Prev { get; set; }

    public async Task<ActionResult> OnGet()
    {
        //Replace with an authorization policy check
        if (HttpContext.Connection.IsRemote())
        {
            return NotFound();
        }

        if (sessionManagementService != null)
        {
            UserSessions = await sessionManagementService.QuerySessionsAsync(new SessionQuery
            {
                ResultsToken = Token,
                RequestPriorResults = Prev == "true",
                DisplayName = DisplayNameFilter,
                SessionId = SessionIdFilter,
                SubjectId = SubjectIdFilter
            });
        }

        return Page();
    }

    [BindProperty]
    public string? SessionId { get; set; }

    public async Task<IActionResult> OnPost()
    {
        //Replace with an authorization policy check
        if (HttpContext.Connection.IsRemote())
        {
            return NotFound();
        }

        ArgumentNullException.ThrowIfNull(sessionManagementService);

        await sessionManagementService.RemoveSessionsAsync(new RemoveSessionsContext
        {
            SessionId = SessionId,
        });

        return RedirectToPage("/ServerSideSessions/Index", new { Token, DisplayNameFilter, SessionIdFilter, SubjectIdFilter, Prev });
    }
}
