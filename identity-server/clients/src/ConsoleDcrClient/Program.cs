// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text.Json;
using Clients;
using Duende.IdentityModel.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

// Add ServiceDefaults from Aspire
builder.AddServiceDefaults();

// Register named HttpClient with service discovery support.
// The AddServiceDiscovery extension enables Aspire to resolve the actual endpoint at runtime.
builder.Services.AddHttpClient("SimpleApi", client =>
{
    client.BaseAddress = new Uri("https://simple-api");
})
.AddServiceDiscovery();

var clientId = Guid.NewGuid().ToString();
var clientSecret = Guid.NewGuid().ToString();

// Register named HttpClient with service discovery support.
// The AddServiceDiscovery extension enables Aspire to resolve the actual endpoint at runtime.
builder.Services.AddHttpClient("SimpleApi", client =>
{
    client.BaseAddress = new Uri("https://simple-api");
})
.AddServiceDiscovery();

// Build the host so we can resolve the HttpClientFactory.
var host = builder.Build();
var httpClientFactory = host.Services.GetRequiredService<IHttpClientFactory>();

await RegisterClient();

var response = await RequestTokenAsync();
response.Show();

await CallServiceAsync(response.AccessToken);

// Graceful shutdown
Environment.Exit(0);

async Task RegisterClient()
{
    // Resolve the authority from the configuration.
    var authority = builder.Configuration["is-host"];

    var client = new HttpClient();

    var request = new DynamicClientRegistrationRequest
    {
        Address = authority + "/connect/dcr",
        Document = new DynamicClientRegistrationDocument
        {

            GrantTypes = { "client_credentials" },
            Scope = "resource1.scope1 resource2.scope1 IdentityServerApi"
        }
    };

    var json = JsonDocument.Parse(
        $$"""
        {
          "client_id": "{{clientId}}",
          "client_secret": "{{clientSecret}}"
        }
        """
    );

    var clientJson = json.RootElement.GetProperty("client_id");
    var secretJson = json.RootElement.GetProperty("client_secret");

    request.Document.Extensions!.Add("client_id", clientJson);
    request.Document.Extensions.Add("client_secret", secretJson);

    var response = await client.RegisterClientAsync(request);

    if (response.IsError)
    {
        Console.WriteLine(response.Error);
        return;
    }

    Console.WriteLine(JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true }));
}

async Task<TokenResponse> RequestTokenAsync()
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
        ClientSecret = clientSecret,
    });

    if (response.IsError)
    {
        Console.WriteLine("\n\nError:\n{0}", response.Error);
        Environment.Exit(-1);
        return null;
    }
    return response;
}

async Task CallServiceAsync(string token)
{
    // Resolve the HttpClient from DI.
    var client = httpClientFactory.CreateClient("SimpleApi");

    client.SetBearerToken(token);
    var response = await client.GetStringAsync("identity");

    "\nService claims:".ConsoleGreen();
    Console.WriteLine(response.PrettyPrintJson());
}
