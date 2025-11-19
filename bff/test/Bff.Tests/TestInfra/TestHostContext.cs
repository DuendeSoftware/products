// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Diagnostics;
using System.Text;
using Xunit.Abstractions;

namespace Duende.Bff.Tests.TestInfra;

public record TestHostContext(ITestOutputHelper OutputHelper)
{
    Stopwatch _watch = Stopwatch.StartNew();
    public readonly SimulatedInternet Internet = new SimulatedInternet(OutputHelper.WriteLine);
    public WriteTestOutput WriteOutput => s =>
    {
        OutputHelper.WriteLine(_watch.ElapsedMilliseconds.ToString() + "ms - " + s);
        LogMessages.AppendLine(s);
    };
    public readonly TestData The = new TestData();
    public TestDataBuilder Some => new TestDataBuilder(The);

    public readonly StringBuilder LogMessages = new StringBuilder();

}
