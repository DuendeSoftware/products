// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Clients;
using Duende.IdentityModel.Client;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

// Resolve the authority from the configuration.
var authority = builder.Configuration["is-host"];

// Add ServiceDefaults from Aspire
builder.AddServiceDefaults();

var _tokenClient = new HttpClient();
var _cache = new DiscoveryCache(authority);

var response = await RequestTokenAsync();
response.Show();

await GetClaimsAsync(response.AccessToken);

// Graceful shutdown
Environment.Exit(0);

async Task<TokenResponse> RequestTokenAsync()
{
    var disco = await _cache.GetAsync();
    if (disco.IsError) throw new Exception(disco.Error);

    var response = await _tokenClient.RequestPasswordTokenAsync(new PasswordTokenRequest
    {
        Address = disco.TokenEndpoint,

        ClientId = "roclient",
        ClientSecret = "secret",

        UserName = "bob",
        Password = "bob",

        Scope = "openid custom.profile"
    });

    if (response.IsError) throw new Exception(response.Error);
    return response;
}

async Task GetClaimsAsync(string token)
{
    var disco = await _cache.GetAsync();
    if (disco.IsError) throw new Exception(disco.Error);

    var response = await _tokenClient.GetUserInfoAsync(new UserInfoRequest
    {
        Address = disco.UserInfoEndpoint,
        Token = token
    });

    if (response.IsError) throw new Exception(response.Error);

    "\n\nUser claims:".ConsoleGreen();
    foreach (var claim in response.Claims)
    {
        Console.WriteLine("{0}\n {1}", claim.Type, claim.Value);
    }
}
