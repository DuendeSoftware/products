// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text.Json;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Licensing.V2.Diagnostics;
using Duende.IdentityServer.Licensing.V2.Diagnostics.DiagnosticEntries;
using Duende.IdentityServer.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace IdentityServer.UnitTests.Licensing.V2;

public class DiagnosticHostedServiceTests
{
    [Fact]
    public async Task ExecuteAsync_ShouldNotThrowOperationCancelledException()
    {
        var diagnosticSummaryLogger = new NullLogger<DiagnosticSummary>();
        var firstDiagnosticEntry = new TestDiagnosticEntry();
        var secondDiagnosticEntry = new TestDiagnosticEntry();
        var thirdDiagnosticEntry = new TestDiagnosticEntry();
        var entries = new List<IDiagnosticEntry>
        {
            firstDiagnosticEntry,
            secondDiagnosticEntry,
            thirdDiagnosticEntry
        };
        var diagnosticService = new DiagnosticDataService(DateTime.UtcNow, entries);
        var diagnosticSummary = new DiagnosticSummary(diagnosticService, new IdentityServerOptions(), new StubLoggerFactory(diagnosticSummaryLogger));

        var options = Options.Create(new IdentityServerOptions());
        var logger = new NullLogger<DiagnosticHostedService>();

        var service = new DiagnosticHostedService(diagnosticSummary, options, logger);

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(100);

        var exception = await Record.ExceptionAsync(async () =>
        {
            await service.ExecuteForTestOnly(cts.Token);
        });

        exception.ShouldBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldNotThrowWhenDelegatePropertiesAreSet()
    {
        var identityServerOptions = new IdentityServerOptions
        {
            Diagnostics = new DiagnosticOptions { LogFrequency = TimeSpan.FromMilliseconds(10) }
        };
        identityServerOptions.DynamicProviders.PathMatchingCallback = _ => Task.FromResult((string)null!);

        var entry = new IdentityServerOptionsDiagnosticEntry(Options.Create(identityServerOptions));
        var diagnosticService = new DiagnosticDataService(DateTime.UtcNow, [entry]);
        var diagnosticSummary = new DiagnosticSummary(diagnosticService, identityServerOptions, new StubLoggerFactory(new NullLogger<DiagnosticSummary>()));

        var service = new DiagnosticHostedService(diagnosticSummary, Options.Create(identityServerOptions), new NullLogger<DiagnosticHostedService>());

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(100);

        var exception = await Record.ExceptionAsync(async () =>
        {
            await service.ExecuteForTestOnly(cts.Token);
        });

        exception.ShouldBeNull();
    }

    private class TestDiagnosticEntry : IDiagnosticEntry
    {
        public Task WriteAsync(DiagnosticContext context, Utf8JsonWriter writer) => Task.CompletedTask;
    }
}
