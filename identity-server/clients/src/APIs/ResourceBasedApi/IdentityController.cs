// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Mvc;

// TODO: remove pragma?
#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace ResourceBasedApi.Controllers;
#pragma warning restore IDE0130

[Route("identity")]
public partial class IdentityController : ControllerBase
{
    private readonly ILogger<IdentityController> _logger;

    public IdentityController(ILogger<IdentityController> logger) => _logger = logger;

    [HttpGet]
    public ActionResult Get()
    {
        var claims = User.Claims.Select(c => new { c.Type, c.Value }).ToArray();
        if (_logger.IsEnabled(LogLevel.Information))
        {
            LogClaims(_logger, claims);
        }

        return new JsonResult(claims);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "claims: {Claims}")]
    private static partial void LogClaims(ILogger logger, IEnumerable<object> claims);
}
