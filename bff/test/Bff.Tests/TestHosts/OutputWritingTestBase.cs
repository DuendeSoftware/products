// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text;

namespace Duende.Bff.Tests.TestHosts;

public class OutputWritingTestBase(ITestOutputHelper testOutputHelper) : IAsyncLifetime
{
    private readonly StringBuilder _output = new StringBuilder();

    public void WriteLine(string message)
    {
        lock (_output)
        {
            _output.AppendLine(message);
        }
    }

    public virtual ValueTask InitializeAsync() => default;

    public virtual ValueTask DisposeAsync()
    {
        lock (_output)
        {
            testOutputHelper.WriteLine(_output.ToString());
        }


        return default;
    }
}
