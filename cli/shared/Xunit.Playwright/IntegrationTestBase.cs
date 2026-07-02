// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Xunit.Playwright;

public class IntegrationTestBase<THost> : IDisposable where THost : class
{
    private readonly IDisposable _loggingScope;
    private readonly ITestOutputHelper _testOutputHelper = TestContext.Current.TestOutputHelper!;

    public IntegrationTestBase(AppHostFixture<THost> fixture)
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
