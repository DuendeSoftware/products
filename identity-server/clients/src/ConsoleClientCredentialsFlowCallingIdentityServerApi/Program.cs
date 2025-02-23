using Clients;
using Duende.IdentityModel.Client;
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace ConsoleClientCredentialsFlowCallingIdentityServerApi
{
    public class Program
    {
        public static async Task Main()
        {
            Console.Title = "Console Client Credentials Flow calling IdentityServer API";


            "JWT access token".ConsoleBox(ConsoleColor.Green);
            var response = await RequestTokenAsync("client");
            response.Show();

            Console.ReadLine();
            await CallServiceAsync(response.AccessToken);
            Console.ReadLine();
            
            "Reference access token".ConsoleBox(ConsoleColor.Green);
            response = await RequestTokenAsync("client.reference");
            response.Show();
            Console.ReadLine();
            await CallServiceAsync(response.AccessToken);
            Console.ReadLine();

            "No access token (expect failure)".ConsoleBox(ConsoleColor.Green);
            await CallServiceAsync(null);
        }

        static async Task<TokenResponse> RequestTokenAsync(string clientId)
        {
            var client = new HttpClient();

            var disco = await client.GetDiscoveryDocumentAsync(Constants.Authority);
            if (disco.IsError) throw new Exception(disco.Error);

            var response = await client.RequestClientCredentialsTokenAsync(new ClientCredentialsTokenRequest
            {
                Address = disco.TokenEndpoint,

                ClientId = clientId,
                ClientSecret = "secret",
                Scope = "IdentityServerApi"
            });

            if (response.IsError) throw new Exception(response.Error);
            return response;
        }

        static async Task CallServiceAsync(string token)
        {
            var baseAddress = Constants.Authority;

            var client = new HttpClient
            {
                BaseAddress = new Uri(baseAddress)
            };

            if (token is not null) client.SetBearerToken(token);
            try
            {
                var response = await client.GetStringAsync("localApi");
                "\n\nService claims:".ConsoleGreen();
                Console.WriteLine(response.PrettyPrintJson());
            }
            catch (Exception ex)
            {
                ex.Message.ConsoleRed();
            }
        }
    }
}