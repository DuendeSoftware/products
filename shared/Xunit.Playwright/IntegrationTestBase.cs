// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Xunit.Abstractions;

namespace Duende.Xunit.Playwright;

public class IntegrationTestBase : IDisposable
{
    private readonly IDisposable _loggingScope;

    public IntegrationTestBase(ITestOutputHelper output, AppHostFixture fixture)
    {
        Output = output;
        Fixture = fixture;
        _loggingScope = fixture.ConnectLogger(output.WriteLine);
    }

    public AppHostFixture Fixture { get; }

    public ITestOutputHelper Output { get; }

    public void Dispose() => _loggingScope.Dispose();

    public HttpClient CreateHttpClient(string clientName) => Fixture.CreateHttpClient(clientName);
}
