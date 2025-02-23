// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Mvc;

namespace IdentityServerHost;

[Route("localApi")]
public class LocalApiController : ControllerBase
{
    public IActionResult Get()
    {
        var claims = from c in User.Claims select new { c.Type, c.Value };
        return new JsonResult(claims);
    }
}