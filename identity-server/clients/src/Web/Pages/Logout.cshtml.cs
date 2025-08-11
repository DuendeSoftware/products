// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Web.Pages;

public class LogoutModel : PageModel
{
    public SignOutResult OnGet() => SignOut("oidc", "cookie");
}
