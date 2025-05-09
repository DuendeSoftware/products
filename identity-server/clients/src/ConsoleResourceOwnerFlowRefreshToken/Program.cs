// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Clients;
using Duende.IdentityModel.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

// Add ServiceDefaults from Aspire
builder.AddServiceDefaults();

// Register a named HttpClient with service discovery support.
// The AddServiceDiscovery extension enables Aspire to resolve the actual endpoint at runtime.
builder.Services.AddHttpClient("SimpleApi", client =>
{
    client.BaseAddress = new Uri("https://simple-api");
})
.AddServiceDiscovery();

// Build the host so we can resolve the HttpClientFactory.
var host = builder.Build();
var httpClientFactory = host.Services.GetRequiredService<IHttpClientFactory>();

// Resolve the authority from the configuration.
var authority = builder.Configuration["is-host"];

var _tokenClient = new HttpClient();
var _cache = new DiscoveryCache(authority);

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

async Task CallServiceAsync(string token)
{
    // Resolve the HttpClient from DI.
    var client = httpClientFactory.CreateClient("SimpleApi");

    client.SetBearerToken(token);
    var response = await client.GetStringAsync("identity");

    "\nService claims:".ConsoleGreen();
    Console.WriteLine(response.PrettyPrintJson());
}
