// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using Duende.Bff.Tests.TestInfra;
namespace Duende.Bff.Tests.Endpoints;

public class WireupTests : BffTestBase
{
    [Fact]
    public async Task Without_auto_wireup_management_endpoints_are_not_mapped()
    {
        Bff.OnConfigureBffOptions += options =>
        {
            options.AutomaticallyRegisterBffMiddleware = false;
        };
        await InitializeAsync();

        await Bff.BrowserClient.Login(expectedStatusCode: HttpStatusCode.NotFound);
    }
    [Fact]
    public async Task Can_call_map_management_endpoints_with_automapping_when_management_path_has_template()
    {
        AddOrUpdateFrontend(Some.BffFrontend());
        Bff.OnConfigureBffOptions += options =>
        {
            // https://github.com/orgs/DuendeSoftware/discussions/301
            // Turns out there was a bug in the code that prevented this from working - fixed now.
            options.AutomaticallyRegisterBffMiddleware = true;
            options.ManagementBasePath = "/{value}/bff";
        };

        Bff.OnConfigureApp += app =>
        {
            app.MapBffManagementEndpoints();
        };

        await InitializeAsync();

        await Bff.BrowserClient.Login(basePath: "/some_value");
    }

    [Fact]
    public async Task Can_call_map_management_endpoints_with_automapping()
    {
        AddOrUpdateFrontend(Some.BffFrontend());
        Bff.OnConfigureBffOptions += options =>
        {
            options.AutomaticallyRegisterBffMiddleware = true;
            options.ManagementBasePath = "/some_base/bff";
        };

        Bff.OnConfigureApp += app =>
        {
            app.MapBffManagementEndpoints();
        };

        await InitializeAsync();

        await Bff.BrowserClient.Login(basePath: "/some_base");
    }
}
