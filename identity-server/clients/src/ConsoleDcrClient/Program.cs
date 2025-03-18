// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text.Json;
using Clients;
using Duende.IdentityModel.Client;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

// Add ServiceDefaults from Aspire
builder.AddServiceDefaults();

var authority = builder.Configuration["is-host"];
var simpleApi = builder.Configuration["simple-api"];

await RegisterClient();

var response = await RequestTokenAsync();
response.Show();

await CallServiceAsync(response.AccessToken);

// Graceful shutdown
Environment.Exit(0);

async Task RegisterClient()
{
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
        """
        {
          "client_id": "client",
          "client_secret": "secret"
        }
        """
    );

    var clientJson = json.RootElement.GetProperty("client_id");
    var secretJson = json.RootElement.GetProperty("client_secret");

    request.Document.Extensions!.Add("client_id", clientJson);
    request.Document.Extensions.Add("client_secret", secretJson);

    var serialized = JsonSerializer.Serialize(request.Document);
    var deserialized = JsonSerializer.Deserialize<DynamicClientRegistrationDocument>(serialized);
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
    var client = new HttpClient();

    var disco = await client.GetDiscoveryDocumentAsync(authority);
    if (disco.IsError) throw new Exception(disco.Error);

    var response = await client.RequestClientCredentialsTokenAsync(new ClientCredentialsTokenRequest
    {
        Address = disco.TokenEndpoint,

        ClientId = "client",
        ClientSecret = "secret",
    });

    if (response.IsError) throw new Exception(response.Error);
    return response;
}

async Task CallServiceAsync(string token)
{
    var client = new HttpClient
    {
        BaseAddress = new Uri(simpleApi)
    };

    client.SetBearerToken(token);
    var response = await client.GetStringAsync("identity");

    "\nService claims:".ConsoleGreen();
    Console.WriteLine(response.PrettyPrintJson());
}
