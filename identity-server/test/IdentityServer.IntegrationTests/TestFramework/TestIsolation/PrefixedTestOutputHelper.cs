// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.IdentityServer.IntegrationTests.TestFramework.TestIsolation;

/// <summary>
/// Wraps an <see cref="ITestOutputHelper"/> and prefixes every message with
/// <c>[prefix]</c> so log output from multiple servers is easy to distinguish.
/// </summary>
public sealed class PrefixedTestOutputHelper(ITestOutputHelper inner, string prefix) : ITestOutputHelper
{
    public string Output => inner.Output;

    public void Write(string message) => inner.Write($"[{prefix}] {message}");

    public void Write(string format, params object[] args) => inner.Write($"[{prefix}] {format}", args);

    public void WriteLine(string message) => inner.WriteLine($"[{prefix}] {message}");

    public void WriteLine(string format, params object[] args) => inner.WriteLine($"[{prefix}] {format}", args);
}
