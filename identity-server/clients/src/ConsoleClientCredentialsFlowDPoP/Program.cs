// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Cryptography;
using System.Text.Json;
using Clients;
using Duende.AccessTokenManagement;
using Duende.AccessTokenManagement.DPoP;
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
if (disco.IsError)
{
    throw new Exception(disco.Error);
}

builder.Services.AddDistributedMemoryCache();
builder.Services.AddClientCredentialsTokenManagement()
    .AddClient("client", client =>
    {
        client.TokenEndpoint = new Uri(disco.TokenEndpoint ?? throw new InvalidOperationException());
        client.ClientId = ClientId.Parse("client");
        client.ClientSecret = ClientSecret.Parse("secret");
        client.DPoPJsonWebKey = CreateDPoPKey();
    });

builder.Services.AddClientCredentialsHttpClient("test", ClientCredentialsClientName.Parse("client"), config =>
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


static DPoPProofKey CreateDPoPKey()
{
    var key = new RsaSecurityKey(RSA.Create(2048));
    var jwk = JsonWebKeyConverter.ConvertFromRSASecurityKey(key);
    jwk.Alg = "PS256";
    var jwkJson = JsonSerializer.Serialize(jwk);

    return DPoPProofKey.Parse(jwkJson);
}
