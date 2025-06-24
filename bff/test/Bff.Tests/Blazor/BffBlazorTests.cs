// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using Duende.Bff;
using Duende.Bff.Blazor;
using Duende.Bff.Tests.Blazor.Components;
using Duende.Bff.Tests.TestInfra;
using Xunit.Abstractions;

namespace Bff.Tests.Blazor;

public class BffBlazorTests : BffTestBase
{
    public BffBlazorTests(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
    {
        Bff.MapGetForRoot = false;
        Bff.OnConfigureServices += services =>
        {
            services.AddRazorComponents()
                .AddInteractiveServerComponents()
                .AddInteractiveWebAssemblyComponents();

            services.AddCascadingAuthenticationState();
            services.AddAntiforgery();
        };

        Bff.OnConfigureBff += bff =>
        {
            bff.AddBlazorServer()
                .AddServerSideSessions();
        };

        Bff.OnConfigureApp += app =>
        {
            app.UseAntiforgery();
            app.MapRazorComponents<App>()
                .AddInteractiveServerRenderMode()
                .AddInteractiveWebAssemblyRenderMode();
        };
    }

    [Theory, MemberData(nameof(AllSetups))]
    public async Task Can_get_home(BffSetupType setup)
    {
        await ConfigureBff(setup);
        var response = await Bff.BrowserClient.GetAsync("/");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Theory, MemberData(nameof(AllSetups))]
    public async Task Cannot_get_secure_without_loggin_in(BffSetupType setup)
    {
        await ConfigureBff(setup);

        Bff.BrowserClient.RedirectHandler.AutoFollowRedirects = false;
        var response = await Bff.BrowserClient.GetAsync("/secure");
        response.StatusCode.ShouldBe(HttpStatusCode.Found, "this indicates we are redirecting to the login page");
    }

    [Theory, MemberData(nameof(AllSetups))]
    public async Task Can_get_secure_when_logged_in(BffSetupType setup)
    {
        await ConfigureBff(setup);

        await Bff.BrowserClient.Login();
        Bff.BrowserClient.RedirectHandler.AutoFollowRedirects = false;
        var response = await Bff.BrowserClient.GetAsync("/secure");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
