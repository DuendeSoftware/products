// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace Bff.DPoP;

[Route("local")]
public class LocalApiController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;

    public LocalApiController(IHttpClientFactory httpClientFactory) => _httpClientFactory = httpClientFactory;

    [Route("self-contained")]
    [HttpGet]
    public IActionResult SelfContained()
    {
        var data = new
        {
            Message = "Hello from self-contained local API",
            User = User!.FindFirst("name")?.Value ?? User!.FindFirst("sub")!.Value
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
