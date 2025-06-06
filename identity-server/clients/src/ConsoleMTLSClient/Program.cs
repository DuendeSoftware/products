// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Cryptography.X509Certificates;
using Clients;
using Duende.IdentityModel.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

// Add ServiceDefaults from Aspire
builder.AddServiceDefaults();

// Register a named HttpClient with service discovery support.
// The AddServiceDiscovery extension enables Aspire to resolve the actual endpoint at runtime.
builder.Services.AddHttpClient("MtlsApi", client =>
{
    client.BaseAddress = new Uri("https://mtls-api");
})
.ConfigurePrimaryHttpMessageHandler(CreateClientCertificateHandler)
.AddServiceDiscovery();
var host = builder.Build();

var response = await RequestTokenAsync();
response.Show();

await CallServiceAsync(response.AccessToken);

// Graceful shutdown
Environment.Exit(0);

async Task<TokenResponse> RequestTokenAsync()
{
    var client = new HttpClient(CreateClientCertificateHandler());
    var authority = builder.Configuration["is-host"];
    var disco = await client.GetDiscoveryDocumentAsync(authority);
    if (disco.IsError)
    {
        throw new Exception(disco.Error);
    }

    var response = await client.RequestClientCredentialsTokenAsync(new ClientCredentialsTokenRequest
    {
        Address = disco.MtlsEndpointAliases.TokenEndpoint,
        ClientId = "mtls",
        ClientCredentialStyle = ClientCredentialStyle.PostBody,
        Scope = "resource1.scope1"
    });

    if (response.IsError)
    {
        throw new Exception(response.Error);
    }

    return response;
}

async Task CallServiceAsync(string token)
{
    var httpClientFactory = host.Services.GetRequiredService<IHttpClientFactory>();
    var client = httpClientFactory.CreateClient("MtlsApi");
    client.SetBearerToken(token);

    var response = await client.GetStringAsync("identity");

    "\nService claims:".ConsoleGreen();
    Console.WriteLine(response.PrettyPrintJson());
}

static SocketsHttpHandler CreateClientCertificateHandler()
{
    var handler = new SocketsHttpHandler();

    var cert = X509CertificateLoader.LoadPkcs12FromFile(path: "localhost-client.p12", password: "changeit");
    handler.SslOptions.ClientCertificates = new X509CertificateCollection { cert };

    return handler;
}
