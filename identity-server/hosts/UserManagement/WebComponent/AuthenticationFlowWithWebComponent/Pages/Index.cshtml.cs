// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AuthenticationFlowWithWebComponent.Pages;

public class IndexModel : PageModel
{
    public IActionResult OnGet() =>
        User.Identity is not null && User.Identity.IsAuthenticated ? RedirectToPage("/Profile") : Page();
}
