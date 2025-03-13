// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Clients;
using Duende.IdentityModel.Client;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

// Add ServiceDefaults from Aspire
builder.AddServiceDefaults();

"JWT access token".ConsoleBox(ConsoleColor.Green);
var response = await RequestTokenAsync("client");
response.Show();
await CallServiceAsync(response.AccessToken);

"Reference access token".ConsoleBox(ConsoleColor.Green);
response = await RequestTokenAsync("client.reference");
response.Show();
await CallServiceAsync(response.AccessToken);

"No access token (expect failure)".ConsoleBox(ConsoleColor.Green);
await CallServiceAsync(null);

static async Task<TokenResponse> RequestTokenAsync(string clientId)
{
    var client = new HttpClient();

    var disco = await client.GetDiscoveryDocumentAsync(Constants.Authority);
    if (disco.IsError) throw new Exception(disco.Error);

    var response = await client.RequestClientCredentialsTokenAsync(new ClientCredentialsTokenRequest
    {
        Address = disco.TokenEndpoint,

        ClientId = clientId,
        ClientSecret = "secret",
        Scope = "IdentityServerApi"
    });

    if (response.IsError) throw new Exception(response.Error);
    return response;
}

static async Task CallServiceAsync(string token)
{
    var baseAddress = Constants.Authority;

    var client = new HttpClient
    {
        BaseAddress = new Uri(baseAddress)
    };

    if (token is not null) client.SetBearerToken(token);
    try
    {
        var response = await client.GetStringAsync("localApi");
        "\nService claims:".ConsoleGreen();
        Console.WriteLine(response.PrettyPrintJson());
    }
    catch (Exception ex)
    {
        ex.Message.ConsoleRed();
    }
}
