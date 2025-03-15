// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Clients;
using Duende.IdentityModel.Client;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

// Add ServiceDefaults from Aspire
builder.AddServiceDefaults();

var response = await RequestTokenAsync();
response.Show();

await CallServiceAsync(response.AccessToken);

static async Task<TokenResponse> RequestTokenAsync()
{
    var client = new HttpClient();

    var disco = await client.GetDiscoveryDocumentAsync(Constants.Authority);
    if (disco.IsError) throw new Exception(disco.Error);

    var response = await client.RequestClientCredentialsTokenAsync(new ClientCredentialsTokenRequest
    {
        Address = disco.TokenEndpoint,
        ClientId = "parameterized.client",
        ClientSecret = "secret",
        Scope = "transaction:123"
    });

    if (response.IsError) throw new Exception(response.Error);
    return response;
}

static async Task CallServiceAsync(string token)
{
    var baseAddress = Constants.SampleApi;

    var client = new HttpClient { BaseAddress = new Uri(baseAddress) };

    client.SetBearerToken(token);
    var response = await client.GetStringAsync("identity");

    "\nService claims:".ConsoleGreen();
    Console.WriteLine(response.PrettyPrintJson());
}