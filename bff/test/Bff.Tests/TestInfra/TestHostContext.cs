// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Xunit.Abstractions;

namespace Duende.Bff.Tests.TestInfra;

public record TestHostContext(ITestOutputHelper OutputHelper)
{
    public readonly SimulatedInternet Internet = new SimulatedInternet(OutputHelper.WriteLine);
    public WriteTestOutput WriteOutput => OutputHelper.WriteLine;
    public readonly TestData The = new TestData();
    public TestDataBuilder Some => new TestDataBuilder(The);
}
