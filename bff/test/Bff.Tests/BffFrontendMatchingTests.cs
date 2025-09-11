// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using Duende.Bff.DynamicFrontends;
using Duende.Bff.Tests.TestInfra;
using Microsoft.AspNetCore.HttpOverrides;
using Xunit.Abstractions;

namespace Duende.Bff.Tests;

public class BffFrontendMatchingTests : BffTestBase
{
    private static readonly BffFrontendName NoFrontendSelected = BffFrontendName.Parse("no_frontend_selected");

    public BffFrontendMatchingTests(ITestOutputHelper output) : base(output)
    {
        // Add a frontend that should never be matched
        AddOrUpdateFrontend(Some.NeverMatchingFrontEnd());

        Bff.OnConfigureApp += app =>
        {
            app.MapGet("/show-front-end",
                (CurrentFrontendAccessor currentFrontendAccessor) =>
                {
                    if (currentFrontendAccessor.TryGet(out var frontend))
                    {
                        return frontend.Name.ToString();
                    }

                    return NoFrontendSelected.ToString();
                });
        };
    }

    [Fact]
    public async Task When_no_frontend_but_openid_config_then_all_endpoints_are_present()
    {
        Bff.OnConfigureBff += bff => bff.ConfigureOpenIdConnect(The.DefaultOpenIdConnectConfiguration);
        IdentityServer.AddClient(The.ClientId, Bff.Url());
        await InitializeAsync();

        // Remove the never-matching frontend so the default frontend is used
        Bff.Resolve<IFrontendCollection>().Remove(Some.NeverMatchingFrontEnd().Name);

        await Bff.BrowserClient.Login();
        var user = await Bff.BrowserClient.CallUserEndpointAsync();
        user.ShouldNotBeEmpty();
        await Bff.BrowserClient.Logout();
    }

    [Fact]
    public async Task Given_unmatched_frontend_then_default_frontend_is_disabled()
    {
        Bff.OnConfigureBff += bff => bff.ConfigureOpenIdConnect(The.DefaultOpenIdConnectConfiguration);
        IdentityServer.AddClient(The.ClientId, Bff.Url("not_matched/"));
        await InitializeAsync();
        Bff.AddOrUpdateFrontend(Some.BffFrontend().MappedToPath(LocalPath.Parse("not_matched")));

        Bff.BrowserClient.DefaultRequestHeaders.Add("x-csrf", "1");
        await Bff.BrowserClient.Login(expectedStatusCode: HttpStatusCode.NotFound);
        await Bff.BrowserClient.GetAsync("/bff/diagnostics").CheckHttpStatusCode(HttpStatusCode.NotFound);
        await Bff.BrowserClient.GetAsync("/bff/logout").CheckHttpStatusCode(HttpStatusCode.NotFound);
        await Bff.BrowserClient.GetAsync("/bff/user").CheckHttpStatusCode(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Can_match_frontend_on_path()
    {
        await InitializeAsync();

        AddOrUpdateFrontend(Some.BffFrontend() with
        {
            SelectionCriteria = new FrontendSelectionCriteria()
            {
                MatchingPath = The.Path
            }
        });
        var frontend = await GetSelectedFrontend(pathPrefix: The.Path);
        frontend.ShouldBe(The.FrontendName);
    }

    [Fact]
    public async Task When_no_frontend_matched_then_show_frontend_returns_none()
    {
        await InitializeAsync();
        var frontend = await GetSelectedFrontend();
        frontend.ShouldBe(NoFrontendSelected);
    }

    [Fact]
    public async Task Given_single_frontend_then_is_selected()
    {
        await InitializeAsync();
        AddOrUpdateFrontend(Some.BffFrontend());
        var frontend = await GetSelectedFrontend();
        frontend.ShouldBe(The.FrontendName);
    }

    [Fact]
    public async Task Can_select_frontend_based_on_domain_name()
    {
        await InitializeAsync();
        AddOrUpdateFrontend(Some.BffFrontend() with
        {
            SelectionCriteria = new FrontendSelectionCriteria()
            {
                MatchingOrigin = Origin.Parse(The.DomainName)
            }
        });

        Internet.AddCustomHandler(map: The.DomainName, to: Bff);

        var client = Internet.BuildHttpClient(The.DomainName);

        var frontend = await GetSelectedFrontend(client);
        frontend.ShouldBe(The.FrontendName);
    }

    [Fact]
    public async Task Will_also_respect_xforwarded_host()
    {
        Bff.OnConfigureServices += services =>
        {
            services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders = ForwardedHeaders.XForwardedHost;
            });
        };
        await InitializeAsync();
        AddOrUpdateFrontend(Some.BffFrontend() with
        {
            SelectionCriteria = new FrontendSelectionCriteria()
            {
                MatchingOrigin = Origin.Parse(The.DomainName)
            }
        });

        //Internet.AddCustomHandler(map: The.DomainName, to: Bff);

        var client = Internet.BuildHttpClient(Bff.Url());
        client.DefaultRequestHeaders.Add("x-forwarded-host", The.DomainName.Host);
        var frontend = await GetSelectedFrontend(client);
        frontend.ShouldBe(The.FrontendName);
    }

    private async Task<BffFrontendName> GetSelectedFrontend(HttpClient? client = null, string? pathPrefix = null)
    {
        var response = await (client ?? Bff.BrowserClient).GetAsync($"{pathPrefix}/show-front-end")
            .CheckHttpStatusCode();

        var frontend = await response.Content.ReadAsStringAsync();
        return BffFrontendName.Parse(frontend);

    }
}
