// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Reflection;
using Microsoft.Playwright;
using Microsoft.Playwright.Xunit.v3;
using Xunit.v3;

namespace Duende.Xunit.Playwright;

public class Defaults
{
    public static readonly PageGotoOptions PageGotoOptions = new PageGotoOptions()
    { WaitUntil = WaitUntilState.NetworkIdle };
}

[WithTestName]
public class PlaywrightTestBase<THost> : PageTest, IDisposable where THost : class
{
    private readonly IDisposable _loggingScope;
    private readonly ITestOutputHelper _testOutputHelper = TestContext.Current.TestOutputHelper!;

    public PlaywrightTestBase(AppHostFixture<THost> fixture)
    {
        Fixture = fixture;
        _loggingScope = fixture.ConnectLogger(_testOutputHelper.WriteLine);

        if (Fixture.UsingAlreadyRunningInstance)
        {
            _testOutputHelper.WriteLine("Running tests against locally running instance");
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

    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();
        Context.SetDefaultTimeout(60_000);
        await Context.Tracing.StartAsync(new()
        {
            Title = $"{WithTestNameAttribute.CurrentClassName}.{WithTestNameAttribute.CurrentTestName}",
            Screenshots = true,
            Snapshots = true,
            Sources = true
        });
    }

    public override async ValueTask DisposeAsync()
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
        await base.DisposeAsync();
    }

    public override BrowserNewContextOptions ContextOptions() => new()
    {
        Locale = "en-US",
        ColorScheme = ColorScheme.Light,

        // We need to ignore https errors to make this work on the build server.
        // Even though we use dotnet dev-certs https --trust on the build agent,
        // it still claims the certs are invalid.
        IgnoreHTTPSErrors = true,
    };


    public AppHostFixture<THost> Fixture { get; }

    public ITestOutputHelper TestOutputHelper => _testOutputHelper;

    public void Dispose()
    {
        if (!Fixture.UsingAlreadyRunningInstance)
        {
            _testOutputHelper.WriteLine(Environment.NewLine);
            _testOutputHelper.WriteLine(Environment.NewLine);
            _testOutputHelper.WriteLine(Environment.NewLine);
            _testOutputHelper.WriteLine("*************************************************");
            _testOutputHelper.WriteLine("** Startup logs ***");
            _testOutputHelper.WriteLine("*************************************************");
            _testOutputHelper.WriteLine(Fixture.StartupLogs);
        }

        _loggingScope.Dispose();
    }

    public HttpClient CreateHttpClient(string clientName) => Fixture.CreateHttpClient(clientName);
}

public class WithTestNameAttribute : BeforeAfterTestAttribute
{
    public static string CurrentTestName = string.Empty;
    public static string CurrentClassName = string.Empty;

    public override void Before(MethodInfo methodUnderTest, IXunitTest test)
    {
        CurrentTestName = methodUnderTest.Name;
        CurrentClassName = methodUnderTest.DeclaringType!.Name;
    }

    public override void After(MethodInfo methodUnderTest, IXunitTest test)
    {
    }
}
