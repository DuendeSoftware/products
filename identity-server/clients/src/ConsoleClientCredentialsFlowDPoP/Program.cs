// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Cryptography;
using System.Text.Json;
using Clients;
using Duende.IdentityModel.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace ConsoleClientCredentialsFlowDPoP
{
    public class Program
    {
        public static async Task Main()
        {
            Console.Title = "Console Client Credentials Flow DPoP";

            var discoClient = new HttpClient();

            var disco = await discoClient.GetDiscoveryDocumentAsync(Constants.Authority);
            if (disco.IsError) throw new Exception(disco.Error);

            var jwkJson = CreateDPoPKey();
            var client = GetHttpClient(disco.TokenEndpoint, jwkJson);

            var response = await client.GetStringAsync("identity");

            "\n\nService Result:".ConsoleGreen();
            Console.WriteLine(response.PrettyPrintJson());
        }

        private static HttpClient GetHttpClient(string tokenEndpoint, string jwk)
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
                config.BaseAddress = new Uri(Constants.SampleApi);
            });

            var provider = services.BuildServiceProvider();
            var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("test");
            return client;
        }

        private static string CreateDPoPKey()
        {
            var key = new RsaSecurityKey(RSA.Create(2048));
            var jwk = JsonWebKeyConverter.ConvertFromRSASecurityKey(key);
            jwk.Alg = "PS256";
            var jwkJson = JsonSerializer.Serialize(jwk);
            return jwkJson;
        }
    }
}