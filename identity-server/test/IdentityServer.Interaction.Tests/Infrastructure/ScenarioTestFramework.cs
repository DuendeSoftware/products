// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Reflection;
using Xunit.v3;

namespace Duende.IdentityServer.Interaction.Tests.Infrastructure;

/// <summary>
/// Custom xUnit v3 test framework that discovers tests from IScenario implementations
/// in the Scenarios assembly. It finds:
/// 1. [Fact] and [Theory] methods on IScenario classes
/// 2. Commands (from GetCommands()) exposed as test cases
/// </summary>
public sealed class ScenarioTestFramework : XunitTestFramework
{
    protected override ITestFrameworkDiscoverer CreateDiscoverer(Assembly assembly)
    {
        var testAssembly = new XunitTestAssembly(assembly);
        var collectionFactory = new CollectionPerClassTestCollectionFactory(testAssembly);
        return new ScenarioTestFrameworkDiscoverer(testAssembly, collectionFactory);
    }
}
