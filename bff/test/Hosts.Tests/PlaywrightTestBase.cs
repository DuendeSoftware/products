// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Hosts.Tests.TestInfra;
using Microsoft.Playwright;
using Microsoft.Playwright.Xunit;
using Xunit.Abstractions;

namespace Hosts.Tests;

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

    public override BrowserNewContextOptions ContextOptions()
    {
        return new()
        {
            Locale = "en-US",
            ColorScheme = ColorScheme.Light,

            // We need to ignore https errors to make this work on the build server. 
            // Even though we use dotnet dev-certs https --trust on the build agent,
            // it still claims the certs are invalid. 
            IgnoreHTTPSErrors = true,
            
        };
    }


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

    public HttpClient CreateHttpClient(string clientName)
    {
        return Fixture.CreateHttpClient(clientName);
    }
}