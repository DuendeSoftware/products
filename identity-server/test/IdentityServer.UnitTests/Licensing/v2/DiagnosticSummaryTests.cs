// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text.Json;
using Duende.IdentityServer.Licensing.V2.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;

namespace IdentityServer.UnitTests.Licensing.V2;

public class DiagnosticSummaryTests
{
    [Fact]
    public async Task PrintSummary_ShouldCallWriteAsyncOnEveryDiagnosticEntry()
    {
        var fakeLogger = new NullLogger<DiagnosticSummary>();
        var firstDiagnosticEntry = new TestDiagnosticEntry();
        var secondDiagnosticEntry = new TestDiagnosticEntry();
        var thirdDiagnosticEntry = new TestDiagnosticEntry();
        var entries = new List<IDiagnosticEntry>
        {
            firstDiagnosticEntry,
            secondDiagnosticEntry,
            thirdDiagnosticEntry
        };
        var summary = new DiagnosticSummary(entries, fakeLogger);

        await summary.PrintSummary();

        firstDiagnosticEntry.WasCalled.ShouldBeTrue();
        secondDiagnosticEntry.WasCalled.ShouldBeTrue();
        thirdDiagnosticEntry.WasCalled.ShouldBeTrue();
    }

    private class TestDiagnosticEntry : IDiagnosticEntry
    {
        public bool WasCalled { get; private set; }
        public Task WriteAsync(Utf8JsonWriter writer)
        {
            WasCalled = true;
            return Task.CompletedTask;
        }
    }
}
