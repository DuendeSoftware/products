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

var discoClient = new HttpClient();
var disco = await discoClient.GetDiscoveryDocumentAsync(authority);
if (disco.IsError) throw new Exception(disco.Error);

builder.Services.AddDistributedMemoryCache();
builder.Services.AddClientCredentialsTokenManagement()
    .AddClient("client", client =>
    {
        client.TokenEndpoint = disco.TokenEndpoint;
        client.ClientId = "client";
        client.ClientSecret = "secret";
        client.DPoPJsonWebKey = CreateDPoPKey();
    });

builder.Services.AddClientCredentialsHttpClient("test", "client", config =>
    {
        config.BaseAddress = new Uri("https://dpop-api");
    })
    .AddServiceDiscovery();

var host = builder.Build();

var client = host.Services.GetRequiredService<IHttpClientFactory>().CreateClient("test");


var response = await client.GetStringAsync("identity");

"\n\nService Result:".ConsoleGreen();
Console.WriteLine(response.PrettyPrintJson());

// Graceful shutdown
Environment.Exit(0);


static string CreateDPoPKey()
{
    var key = new RsaSecurityKey(RSA.Create(2048));
    var jwk = JsonWebKeyConverter.ConvertFromRSASecurityKey(key);
    jwk.Alg = "PS256";
    var jwkJson = JsonSerializer.Serialize(jwk);
    return jwkJson;
}
