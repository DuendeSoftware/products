// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Isolated
{
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
                var userName = User.FindFirst("name");
                message = $"Hello user, {userName.Value}";
            }
            else
            {
                var client = User.FindFirst("client_id");
                message = $"Hello client, {client.Value}";
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
}
