// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Clients;
using ConsoleResourceIndicators;
using Duende.IdentityModel.Client;
using Duende.IdentityModel.OidcClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

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

"Signing in with OIDC".ConsoleBox(ConsoleColor.Green);
"Login window will open in 5 seconds...".ConsoleGreen();
Thread.Sleep(millisecondsTimeout: 5000);

OidcClient _oidcClient;

await SignIn();

// Graceful shutdown
Environment.Exit(0);

async Task SignIn()
{
    // Resolve the authority from the configuration.
    var authority = builder.Configuration["is-host"];

    // Create a redirect URI using an available port on the loopback address.
    // requires the OP to allow random ports on 127.0.0.1 - otherwise set a static port
    var browser = new SystemBrowser();
    var redirectUri = $"http://127.0.0.1:{browser.Port}";

    var options = new OidcClientOptions
    {
        Authority = authority,
        ClientId = "console.pkce",
        RedirectUri = redirectUri,
        Scope = "openid profile resource1.scope1",
        FilterClaims = false,
        Browser = browser
    };

    var serilog = new LoggerConfiguration()
        .MinimumLevel.Error()
        .Enrich.FromLogContext()
        .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level}] {SourceContext}{NewLine}{Message}{NewLine}{Exception}{NewLine}")
        .CreateLogger();

    options.LoggerFactory.AddSerilog(serilog);

    _oidcClient = new OidcClient(options);
    var result = await _oidcClient.LoginAsync(new LoginRequest());

    ShowResult(result);
    await NextSteps(result);
}

void ShowResult(LoginResult result)
{
    if (result.IsError)
    {
        Console.WriteLine("\n\nError:\n{0}", result.Error);
        return;
    }

    Console.WriteLine("\n\nClaims:");
    foreach (var claim in result.User.Claims)
    {
        Console.WriteLine("{0}: {1}", claim.Type, claim.Value);
    }

    Console.WriteLine($"\nidentity token: {result.IdentityToken}");
    Console.WriteLine($"access token:   {result.AccessToken}");
    Console.WriteLine($"refresh token:  {result?.RefreshToken ?? "none"}");
}

async Task NextSteps(LoginResult result)
{
    var currentAccessToken = result.AccessToken;
    var currentRefreshToken = result.RefreshToken;

    // Call API with the current access token.
    await CallApi(currentAccessToken);
    Thread.Sleep(millisecondsTimeout: 2000);

    // Refresh token if available.
    if (currentRefreshToken != null)
    {
        var refreshResult = await _oidcClient.RefreshTokenAsync(currentRefreshToken);
        if (refreshResult.IsError)
        {
            Console.WriteLine($"Error: {refreshResult.Error}");
        }
        else
        {
            currentRefreshToken = refreshResult.RefreshToken;
            currentAccessToken = refreshResult.AccessToken;

            Console.WriteLine("\n\n");
            Console.WriteLine($"access token:   {currentAccessToken}");
            Console.WriteLine($"refresh token:  {currentRefreshToken ?? "none"}");

            // Call API again using the new access token.
            await CallApi(currentAccessToken);
        }
    }

    // Exit the application.
    "Exiting application...".ConsoleYellow();
    Environment.Exit(0);
}

async Task CallApi(string currentAccessToken)
{
    // Resolve the HttpClient from DI.
    var _apiClient = httpClientFactory.CreateClient("SimpleApi");

    _apiClient.SetBearerToken(currentAccessToken);
    var response = await _apiClient.GetAsync("identity");

    if (response.IsSuccessStatusCode)
    {
        var json = await response.Content.ReadAsStringAsync();
        Console.WriteLine("API Response:");
        Console.WriteLine(json);
    }
    else
    {
        Console.WriteLine($"Error: {response.ReasonPhrase}");
    }
}
