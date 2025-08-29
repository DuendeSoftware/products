// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Xunit.Abstractions;

namespace Duende.Xunit.Playwright;

public class IntegrationTestBase<THost> : IDisposable where THost : class
{
    private readonly IDisposable _loggingScope;

    public IntegrationTestBase(ITestOutputHelper output, AppHostFixture<THost> fixture)
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

    public AppHostFixture<THost> Fixture { get; }

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
