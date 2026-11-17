// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MfaStepUp.Pages;

[Authorize(Policy = "MfaRequired")]
public class SpecialAccessModel : PageModel;
