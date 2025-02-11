// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Mvc;

namespace Bff.EF
{
    [Route("local")]
    public class LocalApiController : ControllerBase
    {
        public IActionResult Get()
        {
            var data = new
            {
                Message = "Hello from local API",
                User = User.FindFirst("name")?.Value ?? User.FindFirst("sub").Value
            };

            return Ok(data);
        }
    }
}