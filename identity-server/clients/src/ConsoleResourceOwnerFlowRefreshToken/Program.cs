// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Clients;
using Duende.IdentityModel.Client;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

// Add ServiceDefaults from Aspire
builder.AddServiceDefaults();

var _tokenClient = new HttpClient();
var _cache = new DiscoveryCache(Constants.Authority);

var response = await RequestTokenAsync();
response.Show();

var refresh_token = response.RefreshToken;

while (true)
{
    response = await RefreshTokenAsync(refresh_token);
    response.Show();

    Thread.Sleep(5000);

    await CallServiceAsync(response.AccessToken);

    if (response.RefreshToken != refresh_token)
    {
        refresh_token = response.RefreshToken;
    }
}

async Task<TokenResponse> RequestTokenAsync()
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

    if (response.IsError)
    {
        throw new Exception(response.Error);
    }

    return response;
}

async Task<TokenResponse> RefreshTokenAsync(string refreshToken)
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

    if (response.IsError)
    {
        throw new Exception(response.Error);
    }

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

    "\nService claims:".ConsoleGreen();
    Console.WriteLine(response.PrettyPrintJson());
}
