// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text.Json;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Web.Pages;

public class CallApiModel(IHttpClientFactory factory) : PageModel
{
    public async Task OnGet()
    {
        var client = factory.CreateClient("client");
        var result = await client.GetStringAsync("identity");
        var parsedJson = JsonDocument.Parse(result);
        Json = JsonSerializer.Serialize(parsedJson, new JsonSerializerOptions
        {
            WriteIndented = true
        });
    }

    public string Json { get; set; } = string.Empty;
}
