// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text.Json;
using Clients;
using Duende.IdentityModel.Client;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

// Add ServiceDefaults from Aspire
builder.AddServiceDefaults();

await RegisterClient();

var response = await RequestTokenAsync();
response.Show();

await CallServiceAsync(response.AccessToken);

static async Task RegisterClient()
{
    var client = new HttpClient();

    var request = new DynamicClientRegistrationRequest
    {
        Address = Constants.Authority + "/connect/dcr",
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

static async Task<TokenResponse> RequestTokenAsync()
{
    var client = new HttpClient();

    var disco = await client.GetDiscoveryDocumentAsync(Constants.Authority);
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
