// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Xunit.Sdk;

namespace Duende.Xunit.Playwright.Retries;

[XunitTestCaseDiscoverer(
    typeName: "Duende.Xunit.Playwright.Retries.RetryableTestDiscoverer",
    assemblyName: "Duende.Xunit.Playwright"
)]
public class RetryableFact : FactAttribute
{
    public int MaxRetries { get; set; } = 5;
}
