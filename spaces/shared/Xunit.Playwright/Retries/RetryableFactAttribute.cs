// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Runtime.CompilerServices;
using Xunit.v3;

namespace Duende.Xunit.Playwright.Retries;

/// <summary>
/// Works just like [Fact] except that failures are retried (by default, 5 times).
/// </summary>
[XunitTestCaseDiscoverer(typeof(RetryFactDiscoverer))]
public class RetryableFactAttribute(
    [CallerFilePath] string? sourceFilePath = null,
    [CallerLineNumber] int sourceLineNumber = -1) :
        FactAttribute(sourceFilePath, sourceLineNumber)
{
    public int MaxRetries { get; set; } = 5;
}
