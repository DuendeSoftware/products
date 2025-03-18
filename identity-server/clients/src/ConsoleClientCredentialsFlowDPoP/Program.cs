// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Cryptography;
using System.Text.Json;
using Clients;
using Duende.IdentityModel.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;

var builder = Host.CreateApplicationBuilder(args);

// Add ServiceDefaults from Aspire
builder.AddServiceDefaults();

var authority = builder.Configuration["is-host"];
var simpleApi = builder.Configuration["simple-api"];

var discoClient = new HttpClient();

var disco = await discoClient.GetDiscoveryDocumentAsync(authority);
if (disco.IsError) throw new Exception(disco.Error);

var jwkJson = CreateDPoPKey();
var client = GetHttpClient(disco.TokenEndpoint, jwkJson);

var response = await client.GetStringAsync("identity");

"\n\nService Result:".ConsoleGreen();
Console.WriteLine(response.PrettyPrintJson());

// Graceful shutdown
Environment.Exit(0);

HttpClient GetHttpClient(string tokenEndpoint, string jwk)
{
    var services = new ServiceCollection();
    services.AddDistributedMemoryCache();
    services.AddClientCredentialsTokenManagement()
        .AddClient("client", client =>
        {
            client.TokenEndpoint = tokenEndpoint;
            client.ClientId = "client";
            client.ClientSecret = "secret";
            client.DPoPJsonWebKey = jwk;
        });

    services.AddClientCredentialsHttpClient("test", "client", config =>
    {
        config.BaseAddress = new Uri(simpleApi);
    });

    var provider = services.BuildServiceProvider();
    var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("test");
    return client;
}

static string CreateDPoPKey()
{
    var key = new RsaSecurityKey(RSA.Create(2048));
    var jwk = JsonWebKeyConverter.ConvertFromRSASecurityKey(key);
    jwk.Alg = "PS256";
    var jwkJson = JsonSerializer.Serialize(jwk);
    return jwkJson;
}
