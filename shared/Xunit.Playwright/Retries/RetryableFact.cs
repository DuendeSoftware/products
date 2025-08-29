// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Xunit.Sdk;

namespace Duende.Xunit.Playwright.Retries;

[XunitTestCaseDiscoverer(
    typeName: "Duende.Hosts.Tests.TestInfra.Retries.RetryableTestDiscoverer",
    assemblyName: "Duende.Hosts.Tests"
)]
public class RetryableFact : FactAttribute
{
    public int MaxRetries { get; set; } = 5;
}
