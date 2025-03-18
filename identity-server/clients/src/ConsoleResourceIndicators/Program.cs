// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text;
using Clients;
using ConsoleResourceIndicators;
using Duende.IdentityModel;
using Duende.IdentityModel.Client;
using Duende.IdentityModel.OidcClient;
using Microsoft.Extensions.Hosting;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

// Add ServiceDefaults from Aspire
builder.AddServiceDefaults();

var authority = builder.Configuration["is-host"];

OidcClient _oidcClient;

"Resource Indicators Demo".ConsoleBox(ConsoleColor.Green);

var testsToRun = new List<Test>
{
    new() { Id = "1", Enabled = true, Scope = "resource1.scope1 resource2.scope1 resource3.scope1 shared.scope offline_access" },
    new() { Id = "2", Enabled = true, Scope = "resource1.scope1 resource2.scope1 resource3.scope1 shared.scope" },
    new() { Id = "3", Enabled = true, Scope = "resource1.scope1 resource2.scope1 resource3.scope1 shared.scope offline_access", Resources = ["urn:resource1", "urn:resource2"] },
    new() { Id = "4", Enabled = true, Scope = "resource1.scope1 resource2.scope1 resource3.scope1 shared.scope", Resources = ["urn:resource1", "urn:resource2"] },
    new() { Id = "5", Enabled = true, Scope = "resource1.scope1 resource2.scope1 resource3.scope1 shared.scope offline_access", Resources = ["urn:resource1", "urn:resource2", "urn:resource3"] },
    new() { Id = "6", Enabled = true, Scope = "resource1.scope1 resource2.scope1 resource3.scope1 shared.scope", Resources = ["urn:resource1", "urn:resource2", "urn:resource3"] },
    new() { Id = "7", Enabled = true, Scope = "resource1.scope1 resource2.scope1 resource3.scope1 shared.scope offline_access", Resources = ["urn:resource3"] },
    new() { Id = "8", Enabled = true, Scope = "resource1.scope1 resource2.scope1 resource3.scope1 shared.scope", Resources = ["urn:resource3"] },
    new() { Id = "9", Enabled = true, Scope = "resource3.scope1 offline_access", Resources = ["urn:resource3"] },
    new() { Id = "10", Enabled = true, Scope = "resource3.scope1", Resources = ["urn:resource3"] },
    new() { Id = "11", Enabled = true, Scope = "resource1.scope1 offline_access", Resources = ["urn:resource3"] },
    new() { Id = "12", Enabled = true, Scope = "shared.scope", Resources = ["urn:invalid"] }
};

foreach (var test in testsToRun.Where(t => t.Enabled))
{
    var resources = test.Resources != null ? test.Resources.Aggregate((x, y) => $"{x}, {y}") : "-none-";
    ($"Runing test: ({test.Id}) SCOPES: " + test.Scope + ", RESOURCES: " + resources).ConsoleBox(ConsoleColor.Green);

    try
    {
        await FrontChannel(test.Scope, test.Resources);
        Thread.Sleep(millisecondsTimeout: 1000);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Exception: {ex.Message}");
    }
}

// Exit the application
"Exiting application...".ConsoleYellow();
Environment.Exit(0);

async Task FrontChannel(string scope, IEnumerable<string> resource)
{
    resource ??= [];

    // create a redirect URI using an available port on the loopback address.
    // requires the OP to allow random ports on 127.0.0.1 - otherwise set a static port
    var browser = new SystemBrowser();
    var redirectUri = string.Format($"http://127.0.0.1:{browser.Port}");

    var options = new OidcClientOptions
    {
        Authority = authority,

        ClientId = "console.resource.indicators",

        RedirectUri = redirectUri,
        Scope = scope,
        Resource = [.. resource],
        FilterClaims = false,
        LoadProfile = false,
        Browser = browser,

        Policy =
            {
                RequireIdentityTokenSignature = false
            }
    };

    var serilog = new LoggerConfiguration()
        .MinimumLevel.Warning()
        .Enrich.FromLogContext()
        .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level}] {SourceContext}{NewLine}{Message}{NewLine}{Exception}{NewLine}")
        .CreateLogger();

    options.LoggerFactory.AddSerilog(serilog);

    _oidcClient = new OidcClient(options);
    var result = await _oidcClient.LoginAsync();

    var parts = result.AccessToken.Split('.');
    var header = parts[0];
    var payload = parts[1];

    Console.WriteLine();
    Console.WriteLine("Standard access token:");
    Console.WriteLine(Encoding.UTF8.GetString(Base64Url.Decode(header)).PrettyPrintJson());
    Console.WriteLine(Encoding.UTF8.GetString(Base64Url.Decode(payload)).PrettyPrintJson());

    if (result.RefreshToken == null)
    {
        Console.WriteLine();
        Console.WriteLine("No Refresh Token, exiting.");

        Environment.Exit(0);
    }

    await BackChannel(result);
}

async Task BackChannel(LoginResult result)
{
    Console.WriteLine("\n\n");
    Console.WriteLine("Refreshing with resource parameters");

    var resources = new List<string>() { "urn:resource1", "urn:resource2", "urn:resource3" };

    foreach (var resource in resources)
    {
        $"Refreshing for resource: {resource}...".ConsoleGreen();
        await Refresh(result.RefreshToken, resource);

        Thread.Sleep(millisecondsTimeout: 500);
    }
}

async Task Refresh(string refreshToken, string resource)
{
    var result = await _oidcClient.RefreshTokenAsync(refreshToken,
        new Parameters
        {
            { "resource", resource }
        });

    if (result.IsError)
    {
        Console.WriteLine();
        Console.WriteLine(result.Error);
        return;
    }

    Console.WriteLine();
    Console.WriteLine("down-scoped access token:");

    var parts = result.AccessToken.Split('.');
    var header = parts[0];
    var payload = parts[1];

    Console.WriteLine(Encoding.UTF8.GetString(Base64Url.Decode(header)).PrettyPrintJson());
    Console.WriteLine(Encoding.UTF8.GetString(Base64Url.Decode(payload)).PrettyPrintJson());
}

class Test
{
    public string Id { get; set; }
    public bool Enabled { get; set; }
    public string Scope { get; set; }
    public IEnumerable<string> Resources { get; set; } = null;
}
