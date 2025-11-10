// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Clients;
using Duende.IdentityModel.Client;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

// Add ServiceDefaults from Aspire
builder.AddServiceDefaults();

// Resolve the authority from the configuration.
var authority = builder.Configuration["is-host"];
var Cache = new DiscoveryCache(authority);

"Resource setup:\n".ConsoleGreen();

"resource1: resource1.scope1 resource1.scope2 shared.scope".ConsoleGreen();
"resource2: resource2.scope1 resource2.scope2 shared.scope".ConsoleGreen();
"resource3 (isolated): resource3.scope1 resource3.scope2 shared.scope".ConsoleGreen();
"scopes without resource association: scope3 scope4 transaction\n\n".ConsoleGreen();

// Define a set of automated runs to test the different scenarios
var plannedRuns = new List<PlannedRun>
{
    new() { Enabled = true, Id = "A", Name = "Scopes without associated resource", Scope = "scope3 scope4" },
    new() { Enabled = true, Id = "B", Name = "One scope, single resource", Scope = "resource1.scope1" },
    new() { Enabled = true, Id = "C", Name = "Two scopes, single resources", Scope = "resource1.scope1 resource1.scope2" },
    new() { Enabled = true, Id = "D", Name = "Two scopes, one has a resource, one doesn't", Scope = "resource1.scope1 scope3" },
    new() { Enabled = true, Id = "E", Name = "Two scopes, two resource", Scope = "resource1.scope1 resource2.scope1" },
    new() { Enabled = true, Id = "F", Name = "Shared scope between two resources", Scope = "shared.scope" },
    new() { Enabled = true, Id = "G", Name = "Shared scope between two resources and scope that belongs to resource", Scope = "resource1.scope1 shared.scope" },
    new() { Enabled = true, Id = "H", Name = "Parameterized scope", Scope = "transaction:123" },
    new() { Enabled = true, Id = "I", Name = "No scope", Scope = "" },
    new() { Enabled = true, Id = "J", Name = "No scope (resource: resource1)", Scope = "", Resource = "urn:resource1" },
    new() { Enabled = true, Id = "K", Name = "No scope (resource: resource3)", Scope = "", Resource = "urn:resource3" },
    new() { Enabled = true, Id = "L", Name = "Isolated scope without resource parameter", Scope = "resource3.scope1" },
    new() { Enabled = true, Id = "M", Name = "Isolated scope with resource parameter", Scope = "resource3.scope1", Resource = "urn:resource3" },
    new() { Enabled = true, Id = "N", Name = "Shared scope with resource parameter", Scope = "shared.scope", Resource = "urn:resource2" }
};

// Execute the planned runs
foreach (var run in plannedRuns.Where(t => t.Enabled))
{
    $"Running: ({run.Id}) - {run.Name}".ConsoleBox(ConsoleColor.Green);
    await RequestToken(run);

    Thread.Sleep(millisecondsTimeout: 1000);
}

// Exit the application
"Exiting application...".ConsoleYellow();
Environment.Exit(0);

async Task RequestToken(PlannedRun run)
{
    var client = new HttpClient();
    var disco = await Cache.GetAsync();

    var request = new ClientCredentialsTokenRequest
    {
        Address = disco.TokenEndpoint,
        ClientId = "console.resource.scope",
        ClientSecret = "secret",

        Scope = run.Scope
    };

    if (!string.IsNullOrEmpty(run.Resource))
    {
        request.Resource.Add(run.Resource);
    }

    var response = await client.RequestClientCredentialsTokenAsync(request);

    if (response.IsError)
    {
        Console.WriteLine();

        "An error response was received:".ConsoleRed();
        Console.WriteLine(response.Error);
    }

    Console.WriteLine();
    Console.WriteLine();

    response.Show();
}

internal class PlannedRun
{
    public string Id { get; set; }
    public bool Enabled { get; set; }
    public string Name { get; set; }
    public string Scope { get; set; }
    public string Resource { get; set; } = null;
}
