// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api
{
    [AllowAnonymous]
    public class EchoController : ControllerBase
    {
        [HttpGet("{**catch-all}")]
        public IActionResult Get()
        {
            string message;
            var sub = User.FindFirst("sub");

            if (!User.Identity.IsAuthenticated)
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
            var jti = User.FindFirstValue("jti");

            var response = new
            {
                jti,
                path = Request.Path.Value,
                message,
                time = DateTime.Now.ToString("h:mm:ss.f tt"),
                headers = Request.Headers
            };

            return Ok(response);
        }
    }
}
