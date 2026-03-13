// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.DynamicFrontends;
using Duende.Bff.Tests.TestInfra;
using Xunit.Abstractions;

namespace Duende.Bff.Tests.Diagnostics;

public class FrontendCountDiagnosticEntryTests(ITestOutputHelper testOutputHelper) : BffTestBase(testOutputHelper)
{
    [Fact]
    public async Task Should_print_the_number_of_frontends_during_defined_interval()
    {
        Bff.OnConfigureBffOptions += options =>
        {
            options.Diagnostics.LogFrequency = TimeSpan.FromHours(1);
        };

        await InitializeAsync();

        AdvanceClock(TimeSpan.FromHours(1));

        await WaitForLogMessage("\"FrontendCount\":0");

        AddOrUpdateFrontend(new BffFrontend
        {
            Name = BffFrontendName.Parse("frontend1"),
        });
        AddOrUpdateFrontend(new BffFrontend
        {
            Name = BffFrontendName.Parse("frontend2"),
        });

        AdvanceClock(TimeSpan.FromHours(1));

        await WaitForLogMessage("\"FrontendCount\":2");
    }
}
