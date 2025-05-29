// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Mvc;

namespace MtlsApi.Controllers;

[Route("identity")]
public class IdentityController(ILogger<IdentityController> logger) : ControllerBase
{
    private readonly ILogger<IdentityController> _logger = logger;

    // this action simply echoes the claims back to the client
    [HttpGet]
    public ActionResult Get()
    {
        var claims = User.Claims.Select(c => new { c.Type, c.Value });
        _logger.LogInformation("claims: {claims}", claims);

        return new JsonResult(claims);
    }
}
