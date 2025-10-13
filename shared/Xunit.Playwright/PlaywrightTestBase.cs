// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Reflection;
using Microsoft.Playwright;
namespace Duende.Xunit.Playwright;

public class Defaults
{
    public static readonly PageGotoOptions PageGotoOptions = new PageGotoOptions()
    {
        WaitUntil = WaitUntilState.NetworkIdle
    };
}

[WithTestName]
public class PlaywrightTestBase<THost> : IAsyncLifetime, IDisposable where THost : class
{
    private readonly IDisposable _loggingScope;
    private IPlaywright? _playwright;
    private IBrowser? _browser;

    public PlaywrightTestBase(ITestOutputHelper output, AppHostFixture<THost> fixture)
    {
        Output = output;
        Fixture = fixture;
        _loggingScope = fixture.ConnectLogger(output.WriteLine);

        if (Fixture.UsingAlreadyRunningInstance)
        {
            output.WriteLine("Running tests against locally running instance");
        }
        else
        {
#if DEBUG_NCRUNCH
            // Running in NCrunch. NCrunch cannot build the aspire project, so it needs
            // to be started manually.
            Assert.Skip("When running the Host.Tests using NCrunch, you must start the Hosts.AppHost project manually. IE: dotnet run -p bff/samples/Hosts.AppHost. Or start without debugging from the UI. ");
#endif
        }
    }
    protected IBrowserContext Context { get; private set; } = null!;

    protected IPage Page { get; private set; } = null!;

    public AppHostFixture<THost> Fixture { get; }

    public ITestOutputHelper Output { get; }

    public virtual async ValueTask InitializeAsync()
    {
        _playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new() { Headless = true });
        Context = await _browser.NewContextAsync(ContextOptions());
        Page = await Context.NewPageAsync();

        Context.SetDefaultTimeout(10_000);
        await Context.Tracing.StartAsync(new()
        {
            Title = $"{WithTestNameAttribute.CurrentClassName}.{WithTestNameAttribute.CurrentTestName}",
            Screenshots = true,
            Snapshots = true,
            Sources = true
        });
    }

    public virtual async ValueTask DisposeAsync()
    {
        var path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? Environment.CurrentDirectory;
        // if path ends with /bin/{build configuration}/{dotnetversion}, then strip that from the path.
        var bin = Path.GetFullPath(Path.Combine(path, "../../"));
        if (bin.EndsWith("\\bin\\") || bin.EndsWith("/bin/"))
        {
            path = Path.GetFullPath(Path.Combine(path, "../../../"));
        }

        await Context.Tracing.StopAsync(new()
        {
            Path = Path.Combine(
                path,
                "playwright-traces",
                $"{WithTestNameAttribute.CurrentClassName}.{WithTestNameAttribute.CurrentTestName}.zip"
            )
        });

        await Context.CloseAsync();
        await _browser!.DisposeAsync();
        _playwright!.Dispose();
    }

    public virtual BrowserNewContextOptions ContextOptions() => new()
    {
        Locale = "en-US",
        ColorScheme = ColorScheme.Light,

        // We need to ignore https errors to make this work on the build server.
        // Even though we use dotnet dev-certs https --trust on the build agent,
        // it still claims the certs are invalid.
        IgnoreHTTPSErrors = true,
    };

    public void Dispose()
    {
        if (!Fixture.UsingAlreadyRunningInstance)
        {
            Output.WriteLine(Environment.NewLine);
            Output.WriteLine(Environment.NewLine);
            Output.WriteLine(Environment.NewLine);
            Output.WriteLine("*************************************************");
            Output.WriteLine("** Startup logs ***");
            Output.WriteLine("*************************************************");
            Output.WriteLine(Fixture.StartupLogs);
        }

        _loggingScope.Dispose();
    }

    public HttpClient CreateHttpClient(string clientName) => Fixture.CreateHttpClient(clientName);
}
