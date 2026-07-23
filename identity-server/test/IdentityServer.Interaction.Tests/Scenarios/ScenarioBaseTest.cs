// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Interaction.Infrastructure;
using Duende.IdentityServer.Interaction.Tests.Infrastructure;

namespace Duende.IdentityServer.Interaction.Tests.Scenarios;

public abstract class ScenarioBaseTest<T>(ScenarioFixture<T> fixture, ITestOutputHelper output)
    : IClassFixture<ScenarioFixture<T>>, IAsyncLifetime
    where T : class, IScenario, new()
{
    public ValueTask InitializeAsync()
    {
        fixture.Attach(output);
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        fixture.Detach();
        return ValueTask.CompletedTask;
    }
}
