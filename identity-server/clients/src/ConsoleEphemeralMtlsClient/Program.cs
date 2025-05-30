// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Clients;
using Duende.IdentityModel.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

// Add ServiceDefaults from Aspire
builder.AddServiceDefaults();

var ClientCertificate = CreateClientCertificate("client");

// Register a named HttpClient with service discovery support.
// The AddServiceDiscovery extension enables Aspire to resolve the actual endpoint at runtime.
builder.Services.AddHttpClient("MtlsApi", client =>
    {
        client.BaseAddress = new Uri("https://mtls-api");
    })
    .ConfigurePrimaryHttpMessageHandler(() => CreateClientCertificateHandler(ClientCertificate))
    .AddServiceDiscovery();
var host = builder.Build();

var response = await RequestTokenAsync();
response.Show();

await CallServiceAsync(response.AccessToken);

// Graceful shutdown
Environment.Exit(0);

async Task<TokenResponse> RequestTokenAsync()
{
    var client = new HttpClient(CreateClientCertificateHandler(ClientCertificate));
    var authority = builder.Configuration["is-host"];
    var disco = await client.GetDiscoveryDocumentAsync(authority);
    if (disco.IsError)
    {
        throw new Exception(disco.Error);
    }

    var response = await client.RequestClientCredentialsTokenAsync(new ClientCredentialsTokenRequest
    {
        Address = disco.MtlsEndpointAliases.TokenEndpoint,
        ClientId = "client",
        ClientSecret = "secret",
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

static X509Certificate2 CreateClientCertificate(string name)
{
    var distinguishedName = new X500DistinguishedName($"CN={name}");

    using (var rsa = RSA.Create(2048))
    {
        var request = new CertificateRequest(distinguishedName, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(X509KeyUsageFlags.DataEncipherment | X509KeyUsageFlags.KeyEncipherment | X509KeyUsageFlags.DigitalSignature, false));

        request.CertificateExtensions.Add(
            new X509EnhancedKeyUsageExtension(
                new OidCollection { new Oid("1.3.6.1.5.5.7.3.2") }, false));

        return request.CreateSelfSigned(new DateTimeOffset(DateTime.UtcNow.AddDays(-1)), new DateTimeOffset(DateTime.UtcNow.AddDays(3650)));
    }
}

static SocketsHttpHandler CreateClientCertificateHandler(X509Certificate2 certificate)
{
    var handler = new SocketsHttpHandler
    {
        SslOptions = { ClientCertificates = new X509CertificateCollection { certificate } }
    };

    return handler;
}
