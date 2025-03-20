// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Clients;
using Duende.IdentityModel.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

// Add ServiceDefaults from Aspire
builder.AddServiceDefaults();

// Register a named HttpClient with service discovery support.
// The AddServiceDiscovery extension enables Aspire to resolve the actual endpoint at runtime.
builder.Services.AddHttpClient("IdSrv", client =>
{
    client.BaseAddress = new Uri("https://is-host");
})
.AddServiceDiscovery();

// Build the host so we can resolve the HttpClientFactory.
var host = builder.Build();
var httpClientFactory = host.Services.GetRequiredService<IHttpClientFactory>();

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

// Graceful shutdown
Environment.Exit(0);

async Task<TokenResponse> RequestTokenAsync(string clientId)
{
    // Resolve the authority from the configuration.
    var authority = builder.Configuration["is-host"];

    var client = new HttpClient();

    var disco = await client.GetDiscoveryDocumentAsync(authority);
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

async Task CallServiceAsync(string token)
{
    // Resolve the HttpClient from DI.
    var client = httpClientFactory.CreateClient("IdSrv");

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
