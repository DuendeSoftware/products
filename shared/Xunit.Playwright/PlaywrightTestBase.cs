// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Reflection;
using Microsoft.Playwright;
using Microsoft.Playwright.Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Duende.Xunit.Playwright;

public class Defaults
{
    public static readonly PageGotoOptions PageGotoOptions = new PageGotoOptions()
    { WaitUntil = WaitUntilState.NetworkIdle };
}

[WithTestName]
public class PlaywrightTestBase : PageTest, IDisposable
{
    private readonly IDisposable _loggingScope;

    public PlaywrightTestBase(ITestOutputHelper output, AppHostFixture fixture)
    {
        Output = output;
        Fixture = fixture;
        _loggingScope = fixture.ConnectLogger(output.WriteLine);
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

    public void Dispose() => _loggingScope.Dispose();

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
