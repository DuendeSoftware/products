// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.DPoP;

[AllowAnonymous]
public class EchoController : ControllerBase
{
    [HttpGet("{**catch-all}")]
    public IActionResult Get()
    {
        string message;
        var sub = User.FindFirst("sub");

        if (User.Identity is { IsAuthenticated: false })
        {
            message = "Hello, anonymous caller";
        }
        else if (sub != null)
        {
            var userName = User.FindFirstValue("name");
            message = $"Hello user, {userName}";
        }
        else
        {
            var client = User.FindFirstValue("client_id");
            message = $"Hello client, {client}";
        }

        var response = new
        {
            path = Request.Path.Value,
            message = message,
            time = DateTime.UtcNow.ToString(),
            headers = Request.Headers
        };

        return Ok(response);
    }
}
