// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using Duende.Bff.Blazor;
using Duende.Bff.EntityFramework;
using Duende.Bff.Tests.Blazor.Components;
using Duende.Bff.Tests.TestInfra;
using Microsoft.EntityFrameworkCore;
using UserSessionDb;
using Xunit.Abstractions;

namespace Bff.Tests.Blazor;

public class BffBlazorTests(ITestOutputHelper testOutputHelper) : BffTestBase(testOutputHelper)
{
    private bool _addServerSideSessions = true;
    public override async Task InitializeAsync()
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
            bff.AddBlazorServer();

            if (_addServerSideSessions)
            {
                var dbFilePath = Path.Combine(
                    Path.GetTempPath(),
                    $"test-{Guid.NewGuid():N}.sqlite"
                );
                var connectionString = $"Data Source={dbFilePath}";
                bff.AddEntityFrameworkServerSideSessions(options =>
                    options.UseSqlite(
                        connectionString,
                        dbOpts => dbOpts.MigrationsAssembly(typeof(Startup).Assembly.FullName)
                    )
                );
                bff.AddSessionCleanupBackgroundProcess();
            }
        };

        Bff.OnConfigureApp += app =>
        {
            if (_addServerSideSessions)
            {
                using var scope = app
                    .ApplicationServices.GetRequiredService<IServiceScopeFactory>()
                    .CreateScope();
                scope.ServiceProvider.GetRequiredService<SessionDbContext>().Database.Migrate();
            }

            app.UseAntiforgery();
            app.MapRazorComponents<App>()
                .AddInteractiveServerRenderMode()
                .AddInteractiveWebAssemblyRenderMode();
        };
        await base.InitializeAsync();
    }

    [Theory, MemberData(nameof(AllSetups))]
    public async Task Without_serverside_sessions_add_blazorserver_fails(BffSetupType setup)
    {
        _addServerSideSessions = false;
        var ex = await Should.ThrowAsync<InvalidOperationException>(async () => await ConfigureBff(setup));
        ex.Message.ShouldContain(".AddServerSideSessions()");
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
