// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Cryptography.X509Certificates;
using Clients;
using Duende.IdentityModel.Client;

namespace ConsoleMTLSClient
{
    public class Program
    {
        public static async Task Main()
        {
            Console.Title = "Console mTLS Client";

            var response = await RequestTokenAsync();
            response.Show();

            Console.ReadLine();
            await CallServiceAsync(response.AccessToken);
        }

        static async Task<TokenResponse> RequestTokenAsync()
        {
            var client = new HttpClient(GetHandler());

            var disco = await client.GetDiscoveryDocumentAsync("https://identityserver.local");
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

        static async Task CallServiceAsync(string token)
        {
            var client = new HttpClient(GetHandler())
            {
                BaseAddress = new Uri(Constants.SampleApi)
            };

            client.SetBearerToken(token);
            var response = await client.GetStringAsync("identity");

            "\n\nService claims:".ConsoleGreen();
            Console.WriteLine(response.PrettyPrintJson());
        }

        static SocketsHttpHandler GetHandler()
        {
            var handler = new SocketsHttpHandler();

            var cert = new X509Certificate2("client.p12", "changeit");
            handler.SslOptions.ClientCertificates = new X509CertificateCollection { cert };

            return handler;
        }
    }
}