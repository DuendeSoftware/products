// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PlatformUserManagementApp.Import;

namespace PlatformUserManagementApp.Pages.Import;

[Authorize]
public sealed class IndexModel(IUserImporter importer) : PageModel
{
    public ImportResult? Result { get; set; }
    public string? ErrorMessage { get; set; }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        try
        {
            Result = await importer.ImportFromIdentityDbAsync(HttpContext.RequestAborted);
        }
        catch (NotImplementedException ex)
        {
            ErrorMessage = ex.Message;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Import failed: {ex.Message}";
        }

        return Page();
    }
}
