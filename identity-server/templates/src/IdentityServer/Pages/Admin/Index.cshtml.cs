using Duende.IdentityServer.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace IdentityServerTemplate.Pages.Admin;

[Authorize(Config.Policies.Admin)]
public class Index(DiagnosticDataService? diagnosticDataService = null) : PageModel
{
    public async Task<IActionResult> OnGetDiagnosticsAsync(CancellationToken ct)
    {
        if (diagnosticDataService == null)
        {
            return NotFound();
        }

        var diagnosticsJson = await diagnosticDataService.GetJsonStringAsync(ct);

        Response.Headers.ContentDisposition = "attachment; filename=diagnostics.json";
        return Content(diagnosticsJson, "application/json");
    }
}
