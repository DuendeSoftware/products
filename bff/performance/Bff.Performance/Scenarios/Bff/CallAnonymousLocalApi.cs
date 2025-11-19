// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using NBomber.Contracts;

namespace Bff.Performance.Scenarios.Bff;

public class CallAnonymousLocalApi(Uri baseUri) : BaseScenario(baseUri.ToString())
{
    public override Task Init(IScenarioInitContext c)
    {
        Client = TestClient.Create(baseUri);
        return Task.CompletedTask;
    }

    public override async Task<HttpResponseMessage> RunScenario(IScenarioContext context) => await Client.GetAsync("/local_anon");
}

public class CallAuthorizedLocalApi(Uri baseUri) : BaseScenario(baseUri.ToString())
{
    public override async Task Init(IScenarioInitContext c)
    {
        Client = TestClient.Create(baseUri);
        await Client.TriggerLogin();
    }

    public override async Task<HttpResponseMessage> RunScenario(IScenarioContext context)
    {
        var result = await Client.GetAsync("/local");

        return result;
    }
}
