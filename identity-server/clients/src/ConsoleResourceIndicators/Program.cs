// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using ConsoleResourceIndicators;
using Microsoft.Extensions.Hosting;
using Spectre.Console;

var builder = Host.CreateApplicationBuilder(args);

// Add ServiceDefaults from Aspire
builder.AddServiceDefaults();

// Display banner
AnsiConsole.Write(new Rule("[bold green]Resource Indicators Demo[/]").Centered());
AnsiConsole.WriteLine();

// Resolve the authority from the configuration
var authority = builder.Configuration["is-host"]
    ?? throw new InvalidOperationException("Authority configuration 'is-host' is missing.");

// Display important setup information
var setupPanel = new Panel(
    new Markup($"[yellow]⚠[/] [bold]Before running tests:[/]\n" +
              $"[dim]→[/] Ensure your Identity Server is running at: [cyan]{authority}[/]\n" +
              $"[dim]→[/] Sign in to the Identity Server before starting tests\n" +
              $"[dim]→[/] This will allow tests to complete quickly and smoothly"))
    .Border(BoxBorder.Rounded)
    .BorderColor(Color.Yellow)
    .Header("[yellow]Setup Checklist[/]");

AnsiConsole.Write(setupPanel);
AnsiConsole.WriteLine();

// Determine output mode based on whether console is interactive
OutputMode mode;

if (Console.IsInputRedirected || Console.IsOutputRedirected || !Environment.UserInteractive)
{
    // Non-interactive environment, use verbose mode by default
    AnsiConsole.MarkupLine("[dim]Running in non-interactive mode. Using verbose output.[/]");
    mode = OutputMode.Verbose;
}
else
{
    // Interactive environment, prompt user for output mode
    var outputMode = AnsiConsole.Prompt(
        new SelectionPrompt<string>()
            .Title("[cyan]Choose output mode:[/]")
            .AddChoices("Table View (Live Status)", "Verbose Output (Detailed)")
            .HighlightStyle(new Style(Color.Green)));

    mode = outputMode.StartsWith("Table") ? OutputMode.Table : OutputMode.Verbose;
}

AnsiConsole.WriteLine();

var testRunner = new TestRunner(authority, mode);

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
    new() { Id = "11", Enabled = true, Scope = "resource1.scope1 offline_access", Resources = ["urn:resource3"], AccessTokenExpected = false },
    new() { Id = "12", Enabled = true, Scope = "shared.scope", Resources = ["urn:invalid"], AccessTokenExpected = false }
};

await testRunner.RunAllTestsAsync(testsToRun);

// Show summary
AnsiConsole.WriteLine();
AnsiConsole.Write(new Rule("[bold green]Test Summary[/]").Centered());

var summary = new Table()
    .Border(TableBorder.Rounded)
    .AddColumn("[bold]Status[/]")
    .AddColumn("[bold]Count[/]");

var completed = testsToRun.Count(t => t.Enabled && t.Status == TestStatus.Completed);
var failed = testsToRun.Count(t => t.Enabled && t.Status == TestStatus.Failed);
var total = testsToRun.Count(t => t.Enabled);

summary.AddRow("[green]Completed[/]", completed.ToString());
summary.AddRow("[red]Failed[/]", failed.ToString());
summary.AddRow("[cyan]Total[/]", total.ToString());

AnsiConsole.Write(summary);

// Show expected errors section
var testsWithExpectedErrors = testsToRun
    .Where(t => t.Enabled && t.Result?.RefreshResults.Any(r => r.WasExpectedError) == true)
    .ToList();

if (testsWithExpectedErrors.Any())
{
    AnsiConsole.WriteLine();
    AnsiConsole.Write(new Rule("[bold yellow]Expected Errors (By Design)[/]").Centered());
    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine("[dim]The following errors were expected as part of the test validation:[/]");
    AnsiConsole.WriteLine();

    var expectedErrorsTable = new Table()
        .Border(TableBorder.Rounded)
        .AddColumn("[bold]Test ID[/]")
        .AddColumn("[bold]Resource[/]")
        .AddColumn("[bold]Error[/]")
        .AddColumn("[bold]Reason[/]");

    foreach (var test in testsWithExpectedErrors)
    {
        var expectedErrors = test.Result!.RefreshResults.Where(r => r.WasExpectedError).ToList();

        foreach (var error in expectedErrors)
        {
            var reason = "Resource not configured for this test";

            expectedErrorsTable.AddRow(
                test.Id,
                error.Resource.Replace("urn:", ""),
                $"[yellow]{error.Error ?? "unknown"}[/]",
                $"[dim]{reason}[/]"
            );
        }
    }

    AnsiConsole.Write(expectedErrorsTable);
}

// Show detailed results if available
if (mode == OutputMode.Table)
{
    var testsWithResults = testsToRun.Where(t => t.Enabled && t.Result != null).ToList();

    if (testsWithResults.Any())
    {
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule("[bold cyan]Detailed Test Results[/]").Centered());

        var detailsTable = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("[bold]Test ID[/]")
            .AddColumn("[bold]Access Token[/]")
            .AddColumn("[bold]Refresh Token[/]")
            .AddColumn("[bold]Refresh Operations[/]");

        foreach (var test in testsWithResults)
        {
            var accessToken = test.Result!.AccessTokenReceived
                ? "[green]✓ Received[/]"
                : test.AccessTokenExpected
                    ? "[red]✗ Not Received[/]"
                    : "[green]✓ Not Expected[/]";

            var refreshToken = test.Result.RefreshTokenReceived
                ? "[green]✓ Received[/]"
                : test.RefreshTokenExpected
                    ? "[red]✗ Not Received[/]"
                    : "[green]✓ Not Expected[/]";

            var refreshOps = test.Result.RefreshResults.Any()
                ? $"{test.Result.RefreshResults.Count(r => r.Success)}/{test.Result.RefreshResults.Count} successful"
                : "-";

            detailsTable.AddRow(
                test.Id,
                accessToken,
                refreshToken,
                refreshOps
            );
        }

        AnsiConsole.Write(detailsTable);
    }
}

// Exit prompt - only in interactive mode
if (Environment.UserInteractive && !Console.IsInputRedirected)
{
    AnsiConsole.WriteLine();
    AnsiConsole.Markup("[dim]Press Enter to exit...[/]");
    Console.ReadLine();
}
else
{
    Environment.Exit(0);
}
