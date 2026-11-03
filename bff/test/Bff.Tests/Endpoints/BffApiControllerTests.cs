// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.Configuration;
using Duende.Bff.Tests.TestInfra;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationParts;

namespace Duende.Bff.Tests.Endpoints;

public class BffApiControllerTests : BffTestBase
{
    public BffApiControllerTests()
    {
        Bff.UseBffPlacement = UseBffPlacement.Omitted;
        Bff.OnConfigureServices += services => services
            .AddSingleton(new ApplicationPartManager { ApplicationParts = { new AssemblyPart(typeof(BffApiControllerTests).Assembly) } })
            .AddControllers();
        Bff.OnConfigureApp += app => app.MapControllers();
    }

    [Fact]
    public async Task controller_marked_with_bff_api_attribute_does_not_run_when_bff_middleware_is_missing()
    {
        await ConfigureBff(BffSetupType.V4Bff);

        var ex = await Should.ThrowAsync<InvalidOperationException>(() => Bff.BrowserClient.GetAsync(Bff.Url("/bff-api-controller")));

        ex.Message.ShouldContain("app.UseBff");
    }

    [Fact]
    public async Task controller_without_bff_api_attribute_runs_when_bff_middleware_is_missing()
    {
        await ConfigureBff(BffSetupType.V4Bff);

        _ = await Bff.BrowserClient.GetAsync(Bff.Url("/plain-controller")).CheckHttpStatusCode();
    }
}

[BffApi]
public class BffApiController : ControllerBase
{
    [HttpGet("/bff-api-controller")]
    public IActionResult Get() => Ok();
}

public class PlainController : ControllerBase
{
    [HttpGet("/plain-controller")]
    public IActionResult Get() => Ok();
}
