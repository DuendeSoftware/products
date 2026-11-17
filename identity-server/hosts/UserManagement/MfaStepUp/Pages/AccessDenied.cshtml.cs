// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MfaStepUp.Pages;

[AllowAnonymous]
public class AccessDeniedModel : PageModel;
