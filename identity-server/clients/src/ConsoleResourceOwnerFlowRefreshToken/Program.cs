// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Clients;
using Duende.IdentityModel.Client;

namespace ConsoleResourceOwnerFlowRefreshToken
{
    public class Program
    {
        static HttpClient _tokenClient = new HttpClient();
        static DiscoveryCache _cache = new DiscoveryCache(Constants.Authority);

        static async Task Main()
        {
            Console.Title = "Console ResourceOwner Flow RefreshToken";

            var response = await RequestTokenAsync();
            response.Show();

            Console.ReadLine();

            var refresh_token = response.RefreshToken;

            while (true)
            {
                response = await RefreshTokenAsync(refresh_token);
                response.Show();

                Console.ReadLine();
                await CallServiceAsync(response.AccessToken);

                if (response.RefreshToken != refresh_token)
                {
                    refresh_token = response.RefreshToken;
                }
            }
        }

        static async Task<TokenResponse> RequestTokenAsync()
        {
            var disco = await _cache.GetAsync();

            var response = await _tokenClient.RequestPasswordTokenAsync(new PasswordTokenRequest
            {
                Address = disco.TokenEndpoint,

                ClientId = "roclient",
                ClientSecret = "secret",

                UserName = "bob",
                Password = "bob",

                Scope = "resource1.scope1 offline_access",
            });

            if (response.IsError) throw new Exception(response.Error);
            return response;
        }

        private static async Task<TokenResponse> RefreshTokenAsync(string refreshToken)
        {
            Console.WriteLine("Using refresh token: {0}", refreshToken);

            var disco = await _cache.GetAsync();
            var response = await _tokenClient.RequestRefreshTokenAsync(new RefreshTokenRequest
            {
                Address = disco.TokenEndpoint,

                ClientId = "roclient",
                ClientSecret = "secret",
                RefreshToken = refreshToken
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