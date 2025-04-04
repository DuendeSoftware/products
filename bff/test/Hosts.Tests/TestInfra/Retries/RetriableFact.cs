// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Xunit.Sdk;

namespace Duende.Hosts.Tests.TestInfra.Retries;

[XunitTestCaseDiscoverer(
    typeName: "Duende.Hosts.Tests.TestInfra.Retries.RetriableTestDiscoverer",
    assemblyName: "Duende.Hosts.Tests"
)]
public class RetriableFact : FactAttribute
{
    public int MaxRetries { get; set; } = 5;
}
