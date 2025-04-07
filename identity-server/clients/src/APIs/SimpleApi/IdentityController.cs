// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Mvc;

namespace SampleApi.Controllers;

[Route("identity")]
public class IdentityController : ControllerBase
{
    private readonly ILogger<IdentityController> _logger;

    public IdentityController(ILogger<IdentityController> logger) => _logger = logger;

    [HttpGet]
    public ActionResult Get()
    {
        var claims = User.Claims.Select(c => new { c.Type, c.Value });
        _logger.LogInformation("claims: {claims}", claims);

        return new JsonResult(claims);
    }
}
