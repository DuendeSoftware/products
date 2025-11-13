// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Buffers.Text;
using System.Text;
using Duende.IdentityModel.Client;
using Duende.IdentityModel.OidcClient;
using Spectre.Console;
using Spectre.Console.Json;

namespace ConsoleResourceIndicators;

internal class TestRunner(string authority, OutputMode outputMode)
{
    private readonly string _authority = authority;
    private readonly OutputMode _outputMode = outputMode;
    private OidcClient _oidcClient;

    private static readonly string[] RefreshResources = ["urn:resource1", "urn:resource2", "urn:resource3"];
    private const int DelayBetweenTestsMs = 1000;
    private const int DelayBetweenRefreshMs = 500;

    public async Task RunAllTestsAsync(List<Test> tests)
    {
        if (_outputMode == OutputMode.Table)
        {
            await RunTestsWithTableAsync(tests);
        }
        else
        {
            await RunTestsVerboseAsync(tests);
        }
    }

    private async Task RunTestsVerboseAsync(List<Test> tests)
    {
        foreach (var test in tests.Where(t => t.Enabled))
        {
            await RunTestVerboseAsync(test);
        }
    }

    private async Task RunTestsWithTableAsync(List<Test> tests)
    {
        var enabledTests = tests.Where(t => t.Enabled).ToList();

        await AnsiConsole.Live(CreateTestTable(enabledTests))
            .StartAsync(async ctx =>
            {
                foreach (var test in enabledTests)
                {
                    test.Status = TestStatus.Running;
                    test.StartTime = DateTime.Now;
                    ctx.UpdateTarget(CreateTestTable(enabledTests));

                    try
                    {
                        await ExecuteTestAsync(test, verbose: false);
                        test.Status = TestStatus.Completed;
                    }
                    catch (Exception ex)
                    {
                        test.Status = TestStatus.Failed;
                        test.ErrorMessage = ex.Message;
                    }

                    test.EndTime = DateTime.Now;
                    ctx.UpdateTarget(CreateTestTable(enabledTests));
                    await Task.Delay(DelayBetweenTestsMs);
                }
            });
    }

    private static Table CreateTestTable(List<Test> tests)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("[bold]ID[/]")
            .AddColumn("[bold]Status[/]")
            .AddColumn("[bold]Scopes[/]")
            .AddColumn("[bold]Resources[/]")
            .AddColumn("[bold]Duration[/]");

        foreach (var test in tests)
        {
            var status = test.Status switch
            {
                TestStatus.Pending => "[grey]Pending[/]",
                TestStatus.Running => "[yellow]Running...[/]",
                TestStatus.Completed => "[green]✓ Completed[/]",
                TestStatus.Failed => $"[red]✗ Failed[/]",
                _ => "[grey]Unknown[/]"
            };

            var resourcesList = test.Resources?.Any() == true
                ? string.Join(", ", test.Resources.Select(r => r.Replace("urn:", "")))
                : "-";

            var duration = test.StartTime.HasValue && test.EndTime.HasValue
                ? $"{(test.EndTime.Value - test.StartTime.Value).TotalSeconds:F1}s"
                : test.StartTime.HasValue
                    ? "..."
                    : "-";

            // Truncate scopes for table display
            var scopeDisplay = test.Scope.Length > 40
                ? string.Concat(test.Scope.AsSpan(0, 37), "...")
                : test.Scope;

            table.AddRow(
                test.Id,
                status,
                scopeDisplay,
                resourcesList,
                duration
            );
        }

        return table;
    }

    private async Task RunTestVerboseAsync(Test test)
    {
        var resourcesList = test.Resources?.Any() == true
            ? string.Join(", ", test.Resources)
            : "-none-";

        // Escape the text to prevent Spectre.Console from interpreting it as markup
        var scopeText = test.Scope.EscapeMarkup();
        var resourcesText = resourcesList.EscapeMarkup();

        var panel = new Panel(
            new Markup($"[bold]Test {test.Id}[/]\n" +
                      $"[dim]Scopes:[/] {scopeText}\n" +
                      $"[dim]Resources:[/] {resourcesText}"))
            .Border(BoxBorder.Rounded)
            .BorderColor(Color.Blue)
            .Header("[blue]Running Test[/]");

        AnsiConsole.Write(panel);

        try
        {
            test.Status = TestStatus.Running;
            test.StartTime = DateTime.Now;
            await ExecuteTestAsync(test, verbose: true);
            test.Status = TestStatus.Completed;
            test.EndTime = DateTime.Now;
            AnsiConsole.MarkupLine("[green]✓ Test completed successfully[/]\n");
            await Task.Delay(DelayBetweenTestsMs);
        }
        catch (Exception ex)
        {
            test.Status = TestStatus.Failed;
            test.ErrorMessage = ex.Message;
            test.EndTime = DateTime.Now;
            AnsiConsole.MarkupLine($"[red]✗ Test failed: {Markup.Escape(ex.Message)}[/]\n");
        }
    }

    private async Task ExecuteTestAsync(Test test, bool verbose)
    {
        test.Result = new TestResult();

        var browser = new SystemBrowser();
        var redirectUri = $"http://127.0.0.1:{browser.Port}";

        var options = new OidcClientOptions
        {
            Authority = _authority,
            ClientId = "console.resource.indicators",
            RedirectUri = redirectUri,
            Scope = test.Scope,
            Resource = test.Resources?.ToList() ?? [],
            FilterClaims = false,
            LoadProfile = false,
            Browser = browser,
            Policy =
            {
                RequireIdentityTokenSignature = false
            }
        };

        _oidcClient = new OidcClient(options);
        var result = await _oidcClient.LoginAsync();

        test.Result.AccessTokenReceived = result.AccessToken != null;

        if (verbose)
        {
            HandleAccessTokenVerbose(result.AccessToken, test.AccessTokenExpected);

            if (test.AccessTokenExpected && test.RefreshTokenExpected)
            {
                test.Result.RefreshTokenReceived = result.RefreshToken != null;
                await HandleRefreshTokenVerbose(result, test.Resources, test.Result);
            }
            else if (!test.RefreshTokenExpected)
            {
                AnsiConsole.MarkupLine("[green]✓ Refresh Token was not expected and not received[/]");
            }
        }
        else
        {
            // In table mode, just validate and collect results without output
            if (test.AccessTokenExpected && result.AccessToken == null)
            {
                throw new Exception("Access token expected but not received");
            }

            if (test.AccessTokenExpected && test.RefreshTokenExpected)
            {
                if (result.RefreshToken == null)
                {
                    throw new Exception("Refresh token expected but not received");
                }
                test.Result.RefreshTokenReceived = true;
                await HandleRefreshTokenSilent(result, test.Resources, test.Result);
            }
        }
    }

    private static void HandleAccessTokenVerbose(string accessToken, bool expected)
    {
        if (expected)
        {
            if (accessToken is null)
            {
                AnsiConsole.MarkupLine("[red]✗ An Access Token was expected but not received[/]");
                return;
            }

            AnsiConsole.MarkupLine("[green]✓ Access Token received[/]");
            AnsiConsole.WriteLine();

            PrintJwtToken(accessToken, "Standard Access Token");
        }
        else
        {
            AnsiConsole.MarkupLine("[green]✓ Access Token was not expected and not received[/]");
        }
    }

    private async Task HandleRefreshTokenVerbose(LoginResult result, IEnumerable<string> testResources, TestResult testResult)
    {
        if (result.RefreshToken is null)
        {
            AnsiConsole.MarkupLine("[red]✗ A Refresh Token was expected but not received[/]");
            return;
        }

        AnsiConsole.WriteLine();
        AnsiConsole.WriteLine();

        AnsiConsole.Write(new Rule("[yellow]Refreshing with Resource Parameters[/]").LeftJustified());

        var resourcesSet = testResources?.ToHashSet() ?? [];

        foreach (var resource in RefreshResources)
        {
            AnsiConsole.MarkupLine($"[cyan]→ Refreshing for resource: {resource}[/]");
            await RefreshTokenAsync(result.RefreshToken, resource, resourcesSet.Contains(resource), verbose: true, testResult);
            await Task.Delay(DelayBetweenRefreshMs);
        }
    }

    private async Task HandleRefreshTokenSilent(LoginResult result, IEnumerable<string> testResources, TestResult testResult)
    {
        var resourcesSet = testResources?.ToHashSet() ?? [];

        foreach (var resource in RefreshResources)
        {
            await RefreshTokenAsync(result.RefreshToken!, resource, resourcesSet.Contains(resource), verbose: false, testResult);
            await Task.Delay(DelayBetweenRefreshMs);
        }
    }

    private async Task RefreshTokenAsync(string refreshToken, string resource, bool resourceIsConfigured, bool verbose, TestResult testResult)
    {
        if (_oidcClient == null)
        {
            throw new InvalidOperationException("OIDC client not initialized");
        }

        var result = await _oidcClient.RefreshTokenAsync(refreshToken,
            new Parameters
            {
                { "resource", resource }
            });

        var refreshResult = new RefreshResult
        {
            Resource = resource,
            Success = !result.IsError,
            Error = result.Error,
            WasExpectedError = !resourceIsConfigured && result.IsError
        };

        testResult.RefreshResults.Add(refreshResult);

        if (result.IsError)
        {
            if (resourceIsConfigured)
            {
                var message = $"An error was not expected but was received: {result.Error}";
                if (verbose)
                {
                    AnsiConsole.MarkupLine($"[red]✗ {Markup.Escape(message)}[/]");
                }
                else
                {
                    throw new Exception(message);
                }
            }
            else if (verbose)
            {
                // Expected error - show in verbose mode
                AnsiConsole.MarkupLine($"[green]✓ Expected error received: [/][yellow]{Markup.Escape(result.Error ?? "unknown")}[/]");
            }
            // In non-verbose mode, we don't show expected errors here - they'll be in the summary
            return;
        }

        if (verbose)
        {
            AnsiConsole.WriteLine();
            PrintJwtToken(result.AccessToken!, "Down-scoped access token");
        }
    }

    private static void PrintJwtToken(string token, string blockHeader = "JWT Token")
    {
        var parts = token.Split('.');
        if (parts.Length < 2)
        {
            AnsiConsole.MarkupLine("[red]Invalid JWT token format[/]");
            return;
        }

        var header = parts[0];
        var payload = parts[1];

        var headerJson = Encoding.UTF8.GetString(Base64Url.DecodeFromChars(header));
        var payloadJson = Encoding.UTF8.GetString(Base64Url.DecodeFromChars(payload));

        // Use Spectre.Console's built-in JSON rendering with proper namespace
        AnsiConsole.Write(
            new Panel(
                new Rows(
                    new Markup("[bold yellow]Header:[/]"),
                    new JsonText(headerJson),
                    new Text(""),
                    new Markup("[bold yellow]Payload:[/]"),
                    new JsonText(payloadJson)
                ))
            .Border(BoxBorder.Rounded)
            .BorderColor(Color.Grey)
            .Header($"[dim]{blockHeader}[/]"));
    }
}
