// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Reflection;
using Hosts.Tests.TestInfra;
using Microsoft.Playwright;
using Microsoft.Playwright.Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Hosts.Tests;

[WithTestName]
[Collection(AppHostCollection.CollectionName)]
public class PlaywrightTestBase : PageTest, IDisposable
{
    private readonly IDisposable _loggingScope;

    public PlaywrightTestBase(ITestOutputHelper output, AppHostFixture fixture)
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
            Skip.If(true, "When running the Host.Tests using NCrunch, you must start the Hosts.AppHost project manually. IE: dotnet run -p bff/samples/Hosts.AppHost. Or start without debugging from the UI. ");
#endif
        }
    }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        Context.SetDefaultTimeout(10_000);
        await Context.Tracing.StartAsync(new()
        {
            Title = $"{WithTestNameAttribute.CurrentClassName}.{WithTestNameAttribute.CurrentTestName}",
            Screenshots = true,
            Snapshots = true,
            Sources = true
        });
    }

    public override async Task DisposeAsync()
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


    public AppHostFixture Fixture { get; }

    public ITestOutputHelper Output { get; }

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

public class WithTestNameAttribute : BeforeAfterTestAttribute
{
    public static string CurrentTestName = string.Empty;
    public static string CurrentClassName = string.Empty;

    public override void Before(MethodInfo methodInfo)
    {
        CurrentTestName = methodInfo.Name;
        CurrentClassName = methodInfo.DeclaringType!.Name;
    }

    public override void After(MethodInfo methodInfo)
    {
    }
}
