// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;
using Duende.AccessTokenManagement.OpenIdConnect;
using Microsoft.AspNetCore.Mvc;

namespace Host8;
[Route("local")]
public class LocalApiController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;

    public LocalApiController(IHttpClientFactory httpClientFactory) => _httpClientFactory = httpClientFactory;

    [Route("self-contained")]
    [HttpGet]
    public async Task<IActionResult> SelfContained()
    {
        var ms = HttpContext.RequestServices.GetRequiredService<IUserTokenManagementService>();
        var token = await ms.GetAccessTokenAsync(User, new UserTokenRequestParameters()
        {

        });

        var jwt = new JwtSecurityToken(token.AccessToken);

        var data = new
        {
            Message = "Hello from self-contained local API",
            User = User!.FindFirst("name")?.Value ?? User!.FindFirst("sub")!.Value,
            Email = User.FindFirst("email")?.Value,
            Token = token,
            EmailFromToken = jwt.Claims.FirstOrDefault(x => x.Type == "email")?.Value
        };

        return Ok(data);
    }

    [Route("invokes-external-api")]
    [HttpGet]
    public async Task<IActionResult> InvokesExternalApisAsync()
    {
        var httpClient = _httpClientFactory.CreateClient("api");
        var apiResult = await httpClient.GetAsync("/user-token");
        var content = await apiResult.Content.ReadAsStringAsync();
        var deserialized = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(content);

        var data = new
        {
            Message = "Hello from local API that invokes a remote api",
            RemoteApiResponse = deserialized
        };

        return Ok(data);
    }
}
