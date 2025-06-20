// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using NBomber.Contracts;
using NBomber.CSharp;



namespace Bff.Performance.Scenarios;

public abstract class BaseScenario(string name)
{
    public virtual Task Init(IScenarioInitContext c) => Task.CompletedTask;
    public string Name => GetType().Name + "_" + name;

    public HttpStatusCode SuccessStatusCode { get; set; } = HttpStatusCode.OK;

    public async Task<IResponse> Run(IScenarioContext context)
    {
        var result = await RunScenario(context);

        if (result.StatusCode == SuccessStatusCode)
        {
            return Response.Ok(result.StatusCode, result.StatusCode.ToString(), result.Content.Headers.ContentLength ?? 0);
        }
        return Response.Fail(result.StatusCode, result.StatusCode.ToString(), "Returned an unexpected httpresult", result.Content.Headers.ContentLength ?? 0);
    }

    public abstract Task<HttpResponseMessage> RunScenario(IScenarioContext context);

    public TestClient Client { get; set; } = null!;

    public static implicit operator ScenarioProps(BaseScenario scenario) => Scenario.Create(scenario.Name, scenario.Run)
            .WithoutWarmUp()
            .WithLoadSimulations(
                Simulation.Inject(rate: 10,
                    interval: TimeSpan.FromSeconds(1),
                    during: TimeSpan.FromSeconds(30))
            )
            .WithInit(scenario.Init);
}



