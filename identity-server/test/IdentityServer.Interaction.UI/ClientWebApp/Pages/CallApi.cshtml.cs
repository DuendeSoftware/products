// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text.Json;
using Duende.AccessTokenManagement.OpenIdConnect;
using Duende.IdentityModel.Client;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Duende.IdentityServer.UI.ClientWebApp.Pages;

public class CallApiModel(IHttpClientFactory httpClientFactory, IUserTokenManager? tokenManager = null)
    : PageModel
{
    public string? ApiResponseJson { get; private set; }

    public async Task OnGetAsync()
    {
        var client = httpClientFactory.CreateClient("api");

        if (tokenManager == null)
        {
            // No token management — manually attach bearer token
            var token = await HttpContext.GetTokenAsync("access_token");
            client.SetBearerToken(token!);
        }
        // When token management is registered, the managed HTTP client's
        // delegating handler automatically attaches the access token
        // (including DPoP proofs when configured).

        var response = await client.GetStringAsync("identity");
        ApiResponseJson = PrettyPrintJson(response);
    }

    private static string PrettyPrintJson(string raw)
    {
        var doc = JsonDocument.Parse(raw).RootElement;
        return JsonSerializer.Serialize(doc, new JsonSerializerOptions { WriteIndented = true });
    }
}
