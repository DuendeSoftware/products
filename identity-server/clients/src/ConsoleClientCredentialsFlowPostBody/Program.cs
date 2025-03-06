// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Clients;
using Duende.IdentityModel.Client;

namespace ConsoleClientCredentialsFlow
{
    public class Program
    {
        public static async Task Main()
        {
            Console.Title = "Console ClientCredentials Flow PostBody";

            var response = await RequestTokenAsync();
            response.Show();

            Console.ReadLine();
            await CallServiceAsync(response.AccessToken);
        }

        static async Task<TokenResponse> RequestTokenAsync()
        {
            var client = new HttpClient();

            var disco = await client.GetDiscoveryDocumentAsync(Constants.Authority);
            if (disco.IsError) throw new Exception(disco.Error);

            var response = await client.RequestClientCredentialsTokenAsync(new ClientCredentialsTokenRequest
            {
                Address = disco.TokenEndpoint,
                ClientCredentialStyle = ClientCredentialStyle.PostBody,

                ClientId = "client",
                ClientSecret = "secret",
                Scope = "resource1.scope1"
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

            "\n\nService claims:".ConsoleGreen();
            Console.WriteLine(response.PrettyPrintJson());
        }
    }
}