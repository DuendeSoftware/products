// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text;
using Xunit.Abstractions;

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

    public virtual Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    public virtual Task DisposeAsync()
    {
        lock (_output)
        {
            testOutputHelper.WriteLine(_output.ToString());
        }

        
        return Task.CompletedTask;
    }
}