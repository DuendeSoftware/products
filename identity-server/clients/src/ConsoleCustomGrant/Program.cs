// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Clients;
using Duende.IdentityModel.Client;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

// Add ServiceDefaults from Aspire
builder.AddServiceDefaults();

IDiscoveryCache _cache = new DiscoveryCache(Constants.Authority);

// custom grant type with subject support
var response = await RequestTokenAsync("custom");
response.Show();

await CallServiceAsync(response.AccessToken);

// custom grant type without subject support
response = await RequestTokenAsync("custom.nosubject");
response.Show();

await CallServiceAsync(response.AccessToken);

async Task<TokenResponse> RequestTokenAsync(string grantType)
{
    var client = new HttpClient();

    var disco = await _cache.GetAsync();
    if (disco.IsError) throw new Exception(disco.Error);

    var response = await client.RequestTokenAsync(new TokenRequest
    {
        Address = disco.TokenEndpoint,
        GrantType = grantType,

        ClientId = "client.custom",
        ClientSecret = "secret",

        Parameters =
            {
                { "scope", "resource1.scope1" },
                { "custom_credential", "custom credential"}
            }
    });

    if (response.IsError) throw new Exception(response.Error);
    return response;
}

static async Task CallServiceAsync(string token)
{
    var baseAddress = Constants.SampleApi;

    var client = new HttpClient
    {
        BaseAddress = new Uri(baseAddress)
    };

    client.SetBearerToken(token);
    var response = await client.GetStringAsync("identity");

    "\nService claims:".ConsoleGreen();
    Console.WriteLine(response.PrettyPrintJson());
}
