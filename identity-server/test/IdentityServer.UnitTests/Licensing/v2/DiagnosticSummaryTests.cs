// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text.Json;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Events;
using Duende.IdentityServer.Licensing.V2.Diagnostics;
using Duende.IdentityServer.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Testing;

namespace IdentityServer.UnitTests.Licensing.V2;

public class DiagnosticSummaryTests
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task PrintSummary_ShouldCallWriteAsyncOnEveryDiagnosticEntry()
    {
        var logger = new NullLogger<DiagnosticSummary>();
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
        var summary = new DiagnosticSummary(diagnosticService, new IdentityServerOptions(), new StubLoggerFactory(logger));

        await summary.PrintSummary(_ct);

        firstDiagnosticEntry.WasCalled.ShouldBeTrue();
        secondDiagnosticEntry.WasCalled.ShouldBeTrue();
        thirdDiagnosticEntry.WasCalled.ShouldBeTrue();
    }

    [Fact]
    public async Task PrintSummary_ShouldChunkLargeOutput()
    {
        var chunkSize = 8;
        var options = new IdentityServerOptions { Diagnostics = new DiagnosticOptions { ChunkSize = 8 } };

        var logger = new FakeLogger<DiagnosticSummary>();
        var diagnosticEntry = new LongDiagnosticEntry { OutputLength = chunkSize * 2 };
        var diagnosticService = new DiagnosticDataService(DateTime.UtcNow, [diagnosticEntry]);
        var summary = new DiagnosticSummary(diagnosticService, options, new StubLoggerFactory(logger));

        await summary.PrintSummary(_ct);

        var logSnapshot = logger.Collector.GetSnapshot().Select(x => x.Message);
        logSnapshot.ShouldBe([
            "Diagnostic data (1 of 4): {\"test\":",
            "Diagnostic data (2 of 4): \"xxxxxxx",
            "Diagnostic data (3 of 4): xxxxxxxx",
            "Diagnostic data (4 of 4): x\"}"]);
    }

    [Fact]
    public async Task PrintSummary_ShouldChunkLargeOutputOfMultibyteCharacters()
    {
        var options = new IdentityServerOptions { Diagnostics = new DiagnosticOptions { ChunkSize = 8 } };

        var logger = new FakeLogger<DiagnosticSummary>();
        var diagnosticEntry = new LongDiagnosticEntry { OutputLength = 2, OutputCharacter = '€' };
        var diagnosticService = new DiagnosticDataService(DateTime.UtcNow, [diagnosticEntry]);
        var summary = new DiagnosticSummary(diagnosticService, options, new StubLoggerFactory(logger));


        await summary.PrintSummary(_ct);

        var logSnapshot = logger.Collector.GetSnapshot().Select(x => x.Message);
        logSnapshot.ShouldBe(["Diagnostic data (1 of 3): {\"test\":", "Diagnostic data (2 of 3): \"\\u20AC\\", "Diagnostic data (3 of 3): u20AC\"}"]);
    }

    [Fact]
    public async Task PrintSummary_ShouldCreateChunksWithMaxSizeEightKB()
    {
        var options = new IdentityServerOptions();

        var logger = new FakeLogger<DiagnosticSummary>();
        var diagnosticEntry = new LongDiagnosticEntry { OutputLength = options.Diagnostics.ChunkSize * 2 };
        var diagnosticService = new DiagnosticDataService(DateTime.UtcNow, [diagnosticEntry]);
        var summary = new DiagnosticSummary(diagnosticService, options, new StubLoggerFactory(logger));


        await summary.PrintSummary(_ct);
        foreach (var entry in logger.Collector.GetSnapshot())
        {
            entry.Message.Length.ShouldBeLessThanOrEqualTo(1024 * 8);
        }
    }

    [Fact]
    public async Task PrintSummary_ShouldIncludeLogEventId()
    {
        var options = new IdentityServerOptions();
        var logger = new FakeLogger<DiagnosticSummary>();
        var diagnosticEntry = new LongDiagnosticEntry { OutputLength = 100000 };
        var diagnosticService = new DiagnosticDataService(DateTime.UtcNow, [diagnosticEntry]);
        var summary = new DiagnosticSummary(diagnosticService, options, new StubLoggerFactory(logger));

        await summary.PrintSummary(_ct);

        var logSnapshot = logger.Collector.GetSnapshot();
        logSnapshot.Count.ShouldBeGreaterThan(0);
        logSnapshot[0].Id.Id.ShouldBe(EventIds.DiagnosticSummaryLogged);
    }

    private class TestDiagnosticEntry : IDiagnosticEntry
    {
        public bool WasCalled { get; private set; }
        public Task WriteAsync(DiagnosticContext context, Utf8JsonWriter writer)
        {
            WasCalled = true;
            return Task.CompletedTask;
        }
    }

    private class LongDiagnosticEntry : IDiagnosticEntry
    {
        public int OutputLength { get; set; }
        public char OutputCharacter { get; set; } = 'x';

        public Task WriteAsync(DiagnosticContext context, Utf8JsonWriter writer)
        {
            writer.WriteString("test", new string(OutputCharacter, OutputLength));
            return Task.CompletedTask;
        }
    }
}
