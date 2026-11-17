// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AuthenticationFlowWithWebComponent.Pages;

[Authorize]
public class ShowRecoveryCodesModel : PageModel
{
    [TempData]
    public string[]? RecoveryCodes { get; set; }

    public IActionResult OnGet() =>
        RecoveryCodes is null || RecoveryCodes.Length == 0 ? RedirectToPage("/Profile") : Page();
}
