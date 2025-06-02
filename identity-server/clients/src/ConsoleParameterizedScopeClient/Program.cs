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
builder.Services.AddHttpClient("SimpleApi", client =>
    {
        client.BaseAddress = new Uri("https://simple-api");
    })
    .AddServiceDiscovery();

// Build the host so we can resolve the HttpClientFactory.
var host = builder.Build();
var httpClientFactory = host.Services.GetRequiredService<IHttpClientFactory>();

var response = await RequestTokenAsync();
response.Show();

await CallServiceAsync(response.AccessToken);

async Task<TokenResponse> RequestTokenAsync()
{
    // Resolve the authority from the configuration.
    var authority = builder.Configuration["is-host"];

    var client = new HttpClient();

    var disco = await client.GetDiscoveryDocumentAsync(authority);
    if (disco.IsError)
    {
        throw new Exception(disco.Error);
    }

    var response = await client.RequestClientCredentialsTokenAsync(new ClientCredentialsTokenRequest
    {
        Address = disco.TokenEndpoint,
        ClientId = "parameterized.client",
        ClientSecret = "secret",
        Scope = "transaction:123"
    });

    if (response.IsError)
    {
        throw new Exception(response.Error);
    }

    return response;
}

async Task CallServiceAsync(string token)
{
    // Resolve the HttpClient from DI.
    var _apiClient = httpClientFactory.CreateClient("SimpleApi");

    _apiClient.SetBearerToken(token);
    var response = await _apiClient.GetStringAsync("identity");

    "\nService claims:".ConsoleGreen();
    Console.WriteLine(response.PrettyPrintJson());
}
