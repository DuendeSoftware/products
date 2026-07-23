// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Interaction.Infrastructure;
using Xunit.v3;

namespace Duende.IdentityServer.Interaction.Tests.Infrastructure;

/// <summary>
/// Custom discoverer that augments xUnit's normal type scanning to include
/// IScenario types from the Scenarios assembly. This allows [Fact]/[Theory]
/// methods on scenario classes to be discovered as tests.
/// </summary>
public sealed class ScenarioTestFrameworkDiscoverer : XunitTestFrameworkDiscoverer
{
    public ScenarioTestFrameworkDiscoverer(
        IXunitTestAssembly testAssembly,
        IXunitTestCollectionFactory collectionFactory)
        : base(testAssembly, collectionFactory)
    {
    }

    /// <summary>
    /// Returns the types to scan for tests. Includes the test assembly's own types
    /// plus all exported types from the Scenarios assembly (IScenario implementations,
    /// their nested types, and standalone test classes).
    /// </summary>
    protected override Type[] GetExportedTypes()
    {
        var testAssemblyTypes = base.GetExportedTypes();

        // Get ALL exported types from the Scenarios assembly — this includes
        // IScenario implementations, their nested Tests classes, and standalone
        // test classes like ConsoleFlowTests.
        var scenariosAssembly = typeof(IScenario).Assembly;
        var allScenarioTypes = scenariosAssembly.GetExportedTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false })
            .ToArray();

        // Also include public nested types (e.g. WebClientCodeFlow.Tests)
        var nestedTypes = allScenarioTypes
            .SelectMany(t => t.GetNestedTypes())
            .Where(t => t is { IsAbstract: false, IsInterface: false })
            .ToArray();

        return [.. testAssemblyTypes, .. allScenarioTypes, .. nestedTypes];
    }
}
