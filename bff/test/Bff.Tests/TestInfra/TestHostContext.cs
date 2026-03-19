// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Diagnostics;
using System.Text;
namespace Duende.Bff.Tests.TestInfra;

public record TestHostContext(ITestOutputHelper OutputHelper)
{
    Stopwatch _watch = Stopwatch.StartNew();
    public readonly SimulatedInternet Internet = new SimulatedInternet(OutputHelper.WriteLine);
    public WriteTestOutput WriteOutput => s =>
    {
        OutputHelper.WriteLine(_watch.ElapsedMilliseconds.ToString() + "ms - " + s);
        _ = LogMessages.AppendLine(s);
    };
    public readonly TestData The = new TestData();
    public TestDataBuilder Some => new TestDataBuilder(The);

    public readonly ThreadSafeStringBuilder LogMessages = new();

}

/// <summary>
/// A thread-safe wrapper around <see cref="StringBuilder"/> that synchronizes
/// access to <see cref="AppendLine"/> and <see cref="ToString"/> to prevent
/// race conditions during concurrent logging from test host threads.
/// </summary>
public class ThreadSafeStringBuilder
{
    private readonly StringBuilder _sb = new();
    private readonly object _lock = new();

    public ThreadSafeStringBuilder AppendLine(string value)
    {
        lock (_lock)
        {
            _ = _sb.AppendLine(value);
        }
        return this;
    }

    public override string ToString()
    {
        lock (_lock)
        {
            return _sb.ToString();
        }
    }
}
