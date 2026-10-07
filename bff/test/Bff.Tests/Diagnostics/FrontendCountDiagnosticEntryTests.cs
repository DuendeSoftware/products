// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Diagnostics;
using Duende.Bff.DynamicFrontends;
using Duende.Bff.Tests.TestInfra;
namespace Duende.Bff.Tests.Diagnostics;

public class FrontendCountDiagnosticEntryTests : BffTestBase
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(50);

    [Fact]
    public async Task Should_print_the_number_of_frontends_during_defined_interval()
    {
        Bff.OnConfigureBffOptions += options =>
        {
            options.Diagnostics.LogFrequency = TimeSpan.FromHours(1);
        };

        await InitializeAsync();

        await WaitForBffLogMessageByAdvancingClock("\"FrontendCount\":0");

        AddOrUpdateFrontend(new BffFrontend
        {
            Name = BffFrontendName.Parse("frontend1"),
        });
        AddOrUpdateFrontend(new BffFrontend
        {
            Name = BffFrontendName.Parse("frontend2"),
        });

        await WaitForBffLogMessageByAdvancingClock("\"FrontendCount\":2");
    }

    private async Task WaitForBffLogMessageByAdvancingClock(string message)
    {
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < Timeout)
        {
            AdvanceClock(TimeSpan.FromHours(1));
            await Task.Delay(PollInterval);

            var bffLogMessages = Context.LogMessages
                .ToString()
                .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
                .Where(x => x.StartsWith("bff", StringComparison.Ordinal));

            if (bffLogMessages.Any(x => x.Contains(message, StringComparison.Ordinal)))
            {
                return;
            }
        }

        var finalBffLogMessages = Context.LogMessages
            .ToString()
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
            .Where(x => x.StartsWith("bff", StringComparison.Ordinal));
        finalBffLogMessages.ShouldContain(x => x.Contains(message, StringComparison.Ordinal));
    }
}
