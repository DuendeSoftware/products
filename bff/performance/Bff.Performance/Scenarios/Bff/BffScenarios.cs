// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using NBomber.Contracts;

namespace Bff.Performance.Scenarios.Bff;

public class BffScenarios(Uri baseUri)
{
    public ScenarioProps[] Scenarios =
    [
        new CallAnonymousLocalApi(baseUri),
        new CallAuthorizedLocalApi(baseUri)
    ];
}
