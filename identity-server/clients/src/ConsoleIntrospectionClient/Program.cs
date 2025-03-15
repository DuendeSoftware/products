// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Clients;
using Duende.IdentityModel.Client;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

// Add ServiceDefaults from Aspire
builder.AddServiceDefaults();

IDiscoveryCache _cache = new DiscoveryCache(Constants.Authority);

var response = await RequestTokenAsync();
await IntrospectAsync(response.AccessToken);

async Task<TokenResponse> RequestTokenAsync()
{
    var disco = await _cache.GetAsync();
    if (disco.IsError)
    {
        throw new Exception(disco.Error);
    }

    var client = new HttpClient();
    var response = await client.RequestPasswordTokenAsync(new PasswordTokenRequest
    {
        Address = disco.TokenEndpoint,

        ClientId = "roclient.reference",
        ClientSecret = "secret",

        UserName = "bob",
        Password = "bob",
        Scope = "resource1.scope1 resource2.scope1"
    });

    if (response.IsError)
    {
        throw new Exception(response.Error);
    }

    return response;
}

async Task IntrospectAsync(string accessToken)
{
    var disco = await _cache.GetAsync();
    if (disco.IsError)
    {
        throw new Exception(disco.Error);
    }

    var client = new HttpClient();
    var result = await client.IntrospectTokenAsync(new TokenIntrospectionRequest
    {
        Address = disco.IntrospectionEndpoint,

        ClientId = "urn:resource1",
        ClientSecret = "secret",
        Token = accessToken
    });

    if (result.IsError)
    {
        Console.WriteLine(result.Error);
    }
    else
    {
        if (result.IsActive)
        {
            result.Claims.ToList().ForEach(c => Console.WriteLine($"{c.Type}: {c.Value}"));
        }
        else
        {
            Console.WriteLine("Token is not active.");
        }
    }
}
