// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.DynamicFrontends;
using Duende.Bff.Tests.TestInfra;
using Xunit.Abstractions;

namespace Duende.Bff.Tests;

public class BffFrontendMatchingTests : BffTestBase
{
    private static readonly BffFrontendName NoFrontendSelected = BffFrontendName.Parse("no_frontend_selected");

    public BffFrontendMatchingTests(ITestOutputHelper output) : base(output)
    {
        // Add a frontend that should never be matched
        AddOrUpdateFrontend(Some.NeverMatchingFrontEnd());

        Bff.OnConfigureEndpoints += endpoints =>
        {
            endpoints.MapGet("/show-front-end",
                (SelectedFrontend selectedFrontend) =>
                {
                    if (selectedFrontend.TryGet(out var frontend))
                    {
                        return frontend.Name.ToString();
                    }

                    return NoFrontendSelected.ToString();
                });
        };
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

    private async Task<BffFrontendName> GetSelectedFrontend(HttpClient? client = null, string? pathPrefix = null)
    {
        var response = await (client ?? Bff.BrowserClient).GetAsync($"{pathPrefix}/show-front-end")
            .CheckHttpStatusCode();

        var frontend = await response.Content.ReadAsStringAsync();
        return BffFrontendName.Parse(frontend);

    }
}
