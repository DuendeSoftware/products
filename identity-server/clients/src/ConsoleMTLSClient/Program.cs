// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Cryptography.X509Certificates;
using Clients;
using Duende.IdentityModel.Client;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

// Add ServiceDefaults from Aspire
builder.AddServiceDefaults();

var authority = Constants.AuthorityMtls;
var simpleApi = Constants.SampleApiMtls;

var response = await RequestTokenAsync();
response.Show();

await CallServiceAsync(response.AccessToken);

// Graceful shutdown
Environment.Exit(0);

async Task<TokenResponse> RequestTokenAsync()
{
    var client = new HttpClient(GetHandler());

    var disco = await client.GetDiscoveryDocumentAsync(authority);
    if (disco.IsError) throw new Exception(disco.Error);

    var response = await client.RequestClientCredentialsTokenAsync(new ClientCredentialsTokenRequest
    {
        Address = disco.MtlsEndpointAliases.TokenEndpoint,
        ClientId = "mtls",
        ClientCredentialStyle = ClientCredentialStyle.PostBody,
        Scope = "resource1.scope1"
    });

    if (response.IsError) throw new Exception(response.Error);
    return response;
}

async Task CallServiceAsync(string token)
{
    var client = new HttpClient(GetHandler())
    {
        BaseAddress = new Uri(simpleApi)
    };

    client.SetBearerToken(token);
    var response = await client.GetStringAsync("identity");

    "\nService claims:".ConsoleGreen();
    Console.WriteLine(response.PrettyPrintJson());
}

static SocketsHttpHandler GetHandler()
{
    var handler = new SocketsHttpHandler();

    var cert = X509CertificateLoader.LoadPkcs12FromFile(path: "client.p12", password: "changeit");
    handler.SslOptions.ClientCertificates = new X509CertificateCollection { cert };

    return handler;
}
