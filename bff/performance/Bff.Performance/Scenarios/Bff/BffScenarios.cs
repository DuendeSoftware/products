// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using NBomber.Contracts;

namespace Bff.Performance.Scenarios.Bff;

public class BffScenarios
{
    public ScenarioProps[] Scenarios;

    public BffScenarios(Uri[] baseUris) => Scenarios = baseUris.SelectMany(x => new ScenarioProps[]
                                                {
            new CallAnonymousLocalApi(x),
            new CallAuthorizedLocalApi(x)
                                                })
            .ToArray();
}
